using System.Text.RegularExpressions;
using Kotlet.Application.Ingredients;
using Kotlet.Application.Images;
using Kotlet.Application.Measurements;
using Kotlet.Application.Translations;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public sealed class RecipeService(
    IRecipeRepository repository,
    IIngredientRepository ingredientRepository,
    MeasurementMappingService measurementMappingService,
    RecipeResponseMapper responseMapper,
    IRecipeImageRepository? imageRepository = null,
    StoredImageService? imageStorage = null)
{
    public async Task<PagedResponse<RecipeSummaryResponse>> ListAsync(
        Guid houseId, int page, int pageSize, string? search, string? mealType,
        IReadOnlyCollection<Guid>? ingredientIds, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await repository.GetPagedAsync(
            houseId, page, pageSize, search, ParseMealType(mealType), ingredientIds, cancellationToken);
        var firstImageIds = await GetFirstImageIdsAsync(items.Select(r => r.Id).ToList(), cancellationToken);
        return new PagedResponse<RecipeSummaryResponse>(
            items.Select(r => responseMapper.ToSummaryResponse(r, firstImageIds)).ToList(),
            page, pageSize, total);
    }

    public async Task<IReadOnlyList<RecipeSummaryResponse>> ListRecentAsync(
        Guid houseId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 20);
        var recipes = await repository.GetRecentAsync(houseId, limit, cancellationToken);
        var firstImageIds = await GetFirstImageIdsAsync(recipes.Select(r => r.Id).ToList(), cancellationToken);
        return recipes.Select(r => responseMapper.ToSummaryResponse(r, firstImageIds)).ToList();
    }

    public async Task<RecipeDetailResponse?> GetByIdAsync(
        Guid id, Guid houseId, CancellationToken cancellationToken, string languageCode = TranslationKeys.DefaultLanguage)
    {
        var recipe = await repository.GetByIdAsync(id, houseId, tracked: false, cancellationToken);
        if (recipe is null) return null;
        var images = imageRepository is null
            ? []
            : await imageRepository.ListAsync(id, cancellationToken);
        return await responseMapper.ToDetailResponseAsync(recipe, languageCode, images.Select(RecipeResponseMapper.ToImageResponse).ToList(), canEdit: true, cancellationToken);
    }

    public async Task<RecipeDetailResponse?> GetPublicByIdAsync(
        Guid id, Guid? currentHouseId, CancellationToken cancellationToken, string languageCode = TranslationKeys.DefaultLanguage)
    {
        var recipe = await repository.GetPublicByIdAsync(id, cancellationToken);
        if (recipe is null) return null;
        var images = imageRepository is null
            ? []
            : await imageRepository.ListAsync(id, cancellationToken);
        return await responseMapper.ToDetailResponseAsync(recipe, languageCode, images.Select(RecipeResponseMapper.ToImageResponse).ToList(),
            canEdit: currentHouseId == recipe.HouseId, cancellationToken);
    }

    public async Task<RecipeOperationResult> CreateAsync(
        Guid ownerUserId, Guid houseId, CreateRecipeRequest request, CancellationToken cancellationToken, string languageCode = TranslationKeys.DefaultLanguage)
    {
        var errors = RecipeValidator.Validate(request.Title, request.DescriptionMarkdown, request.Ingredients, request.Servings, request.MealType, request.SourceUrl);
        if (errors.Count > 0)
            return new(RecipeOperationStatus.ValidationFailed, ValidationErrors: errors);

        var mappedIngredients = await MapIngredientsAsync(request.Ingredients, Guid.Empty, cancellationToken);
        if (mappedIngredients.Errors.Count > 0)
            return new(RecipeOperationStatus.ValidationFailed, ValidationErrors: mappedIngredients.Errors);

        var title = request.Title.Trim();
        var baseSlug = GenerateSlug(title);
        if (baseSlug.Length == 0)
            return new(RecipeOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]> { ["title"] = ["Title must contain at least one letter or digit."] });
        var slug = await ResolveSlugAsync(houseId, baseSlug, null, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var recipeId = Guid.NewGuid();
        foreach (var ingredient in mappedIngredients.Items) ingredient.RecipeId = recipeId;
        var recipe = new Recipe
        {
            Id = recipeId,
            HouseId = houseId,
            OwnerUserId = ownerUserId,
            Title = title,
            Slug = slug,
            DescriptionMarkdown = request.DescriptionMarkdown?.Trim(),
            Servings = ServingCount.FromInt32(request.Servings),
            MealType = ParseMealType(request.MealType),
            IsAiAssisted = request.IsAiAssisted,
            SourceUrl = RecipeSourceUrl.Normalize(request.SourceUrl),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Ingredients = mappedIngredients.Items
        };

        repository.Add(recipe);
        await repository.SaveChangesAsync(cancellationToken);
        HydrateIngredientNavigation(mappedIngredients);
        return new(RecipeOperationStatus.Success, await responseMapper.ToDetailResponseAsync(recipe, languageCode, canEdit: true, cancellationToken: cancellationToken));
    }

    public async Task<RecipeOperationResult> UpdateAsync(
        Guid id, Guid houseId, UpdateRecipeRequest request, CancellationToken cancellationToken, string languageCode = TranslationKeys.DefaultLanguage)
    {
        var errors = RecipeValidator.Validate(request.Title, request.DescriptionMarkdown, request.Ingredients, request.Servings, request.MealType, request.SourceUrl);
        if (errors.Count > 0)
            return new(RecipeOperationStatus.ValidationFailed, ValidationErrors: errors);

        var mappedIngredients = await MapIngredientsAsync(request.Ingredients, id, cancellationToken);
        if (mappedIngredients.Errors.Count > 0)
            return new(RecipeOperationStatus.ValidationFailed, ValidationErrors: mappedIngredients.Errors);

        var recipe = await repository.GetByIdAsync(id, houseId, tracked: true, cancellationToken);
        if (recipe is null)
            return new(RecipeOperationStatus.NotFound);

        var title = request.Title.Trim();
        var newSlug = GenerateSlug(title);
        if (newSlug.Length == 0)
            return new(RecipeOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]> { ["title"] = ["Title must contain at least one letter or digit."] });
        if (newSlug != recipe.Slug)
            newSlug = await ResolveSlugAsync(houseId, newSlug, id, cancellationToken);

        recipe.Title = title;
        recipe.Slug = newSlug;
        recipe.DescriptionMarkdown = request.DescriptionMarkdown?.Trim();
        recipe.Servings = ServingCount.FromInt32(request.Servings);
        recipe.MealType = ParseMealType(request.MealType);
        recipe.SourceUrl = RecipeSourceUrl.Normalize(request.SourceUrl);
        recipe.UpdatedAtUtc = DateTimeOffset.UtcNow;
        recipe.Ingredients.Clear();
        foreach (var ing in mappedIngredients.Items)
            recipe.Ingredients.Add(ing);
        await repository.SaveChangesAsync(cancellationToken);
        HydrateIngredientNavigation(mappedIngredients);
        return new(RecipeOperationStatus.Success, await responseMapper.ToDetailResponseAsync(recipe, languageCode, canEdit: true, cancellationToken: cancellationToken));
    }

    public async Task<RecipeOperationStatus> DeleteAsync(
        Guid id, Guid houseId, CancellationToken cancellationToken)
    {
        var recipe = await repository.GetByIdAsync(id, houseId, tracked: true, cancellationToken);
        if (recipe is null)
            return RecipeOperationStatus.NotFound;

        if (imageRepository is not null && imageStorage is not null)
            foreach (var image in await imageRepository.ListAsync(id, cancellationToken))
                await imageStorage.DeleteAsync(image.Id, cancellationToken);
        repository.Remove(recipe);
        await repository.SaveChangesAsync(cancellationToken);
        return RecipeOperationStatus.Success;
    }

    private async Task<MappedIngredients> MapIngredientsAsync(
        IReadOnlyList<RecipeIngredientRequest> requests, Guid recipeId, CancellationToken cancellationToken)
    {
        var ingredientIds = requests.Select(x => x.IngredientId).Distinct().ToArray();
        var ingredients = await ingredientRepository.GetByIdsAsync(ingredientIds, cancellationToken);
        var items = new List<RecipeIngredient>(requests.Count);
        var errors = new List<string>();

        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            if (!ingredients.TryGetValue(request.IngredientId, out var ingredient))
            {
                errors.Add($"Ingredient at position {index + 1}: ingredient does not exist.");
                continue;
            }

            var normalized = measurementMappingService.Normalize(request.Quantity, request.Unit, ingredient);
            if (normalized is null || normalized.Quantity > 999999999.999m)
            {
                errors.Add($"Ingredient at position {index + 1}: measurement is unsupported or exceeds the maximum quantity.");
                continue;
            }

            items.Add(new RecipeIngredient
            {
                RecipeId = recipeId,
                IngredientId = ingredient.Id,
                SortOrder = index,
                NormalizedQuantity = Quantity.FromAmount(normalized.Quantity),
                NormalizedUnit = normalized.Unit,
                Note = request.Note?.Trim()
            });
        }

        return new(items, ingredients, errors.Count == 0
            ? new Dictionary<string, string[]>()
            : new Dictionary<string, string[]> { ["ingredients"] = errors.ToArray() });
    }

    private static void HydrateIngredientNavigation(MappedIngredients mapped)
    {
        foreach (var item in mapped.Items)
            item.Ingredient = mapped.Ingredients[item.IngredientId];
    }

    private async Task<string> ResolveSlugAsync(
        Guid houseId, string baseSlug, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (!await repository.SlugExistsAsync(houseId, baseSlug, excludedId, cancellationToken))
            return baseSlug;

        for (var i = 2; i <= 1000; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!await repository.SlugExistsAsync(houseId, candidate, excludedId, cancellationToken))
                return candidate;
        }
        return $"{baseSlug}-{Guid.NewGuid():N}";
    }

    internal static string GenerateSlug(string title)
    {
        var slug = title.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');
        return slug.Length > 200 ? slug[..200] : slug;
    }

    private async Task<IReadOnlyDictionary<Guid, Guid>> GetFirstImageIdsAsync(
        IReadOnlyList<Guid> recipeIds, CancellationToken cancellationToken)
    {
        if (imageRepository is null || recipeIds.Count == 0)
            return new Dictionary<Guid, Guid>();
        return await imageRepository.GetFirstImageIdsAsync(recipeIds, cancellationToken);
    }

    private static MealSlot? ParseMealType(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : MealSlotValues.TryParse(value, out var slot) ? slot : null;

    private sealed record MappedIngredients(
        List<RecipeIngredient> Items,
        IReadOnlyDictionary<Guid, Ingredient> Ingredients,
        Dictionary<string, string[]> Errors);
}
