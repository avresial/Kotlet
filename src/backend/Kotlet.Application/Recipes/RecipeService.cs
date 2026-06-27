using System.Text.RegularExpressions;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public sealed class RecipeService(IRecipeRepository repository, IRecipeImageRepository? imageRepository = null)
{
    private const int MaxIngredients = 100;

    public async Task<PagedResponse<RecipeSummaryResponse>> ListAsync(
        Guid ownerUserId, int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await repository.GetPagedAsync(ownerUserId, page, pageSize, search, cancellationToken);
        var firstImageIds = await GetFirstImageIdsAsync(items.Select(r => r.Id).ToList(), cancellationToken);
        return new PagedResponse<RecipeSummaryResponse>(
            items.Select(r => ToSummaryResponse(r, firstImageIds)).ToList(),
            page, pageSize, total);
    }

    public async Task<IReadOnlyList<RecipeSummaryResponse>> ListRecentAsync(
        Guid ownerUserId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 20);
        var recipes = await repository.GetRecentAsync(ownerUserId, limit, cancellationToken);
        var firstImageIds = await GetFirstImageIdsAsync(recipes.Select(r => r.Id).ToList(), cancellationToken);
        return recipes.Select(r => ToSummaryResponse(r, firstImageIds)).ToList();
    }

    public async Task<RecipeDetailResponse?> GetByIdAsync(
        Guid id, Guid ownerUserId, CancellationToken cancellationToken)
    {
        var recipe = await repository.GetByIdAsync(id, ownerUserId, tracked: false, cancellationToken);
        if (recipe is null) return null;
        var images = imageRepository is null
            ? []
            : await imageRepository.ListAsync(id, false, cancellationToken);
        return ToDetailResponse(recipe, images.Select(ToImageResponse).ToList());
    }

    public async Task<RecipeOperationResult> CreateAsync(
        Guid ownerUserId, CreateRecipeRequest request, CancellationToken cancellationToken)
    {
        var errors = Validate(request.Title, request.DescriptionMarkdown, request.Ingredients);
        if (errors.Count > 0)
            return new(RecipeOperationStatus.ValidationFailed, ValidationErrors: errors);

        var title = request.Title.Trim();
        var baseSlug = GenerateSlug(title);
        if (baseSlug.Length == 0)
            return new(RecipeOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]> { ["title"] = ["Title must contain at least one letter or digit."] });
        var slug = await ResolveSlugAsync(ownerUserId, baseSlug, null, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var recipeId = Guid.NewGuid();
        var recipe = new Recipe
        {
            Id = recipeId,
            OwnerUserId = ownerUserId,
            Title = title,
            Slug = slug,
            DescriptionMarkdown = request.DescriptionMarkdown?.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Ingredients = MapIngredients(request.Ingredients, recipeId)
        };

        repository.Add(recipe);
        await repository.SaveChangesAsync(cancellationToken);
        return new(RecipeOperationStatus.Success, ToDetailResponse(recipe));
    }

    public async Task<RecipeOperationResult> UpdateAsync(
        Guid id, Guid ownerUserId, UpdateRecipeRequest request, CancellationToken cancellationToken)
    {
        var errors = Validate(request.Title, request.DescriptionMarkdown, request.Ingredients);
        if (errors.Count > 0)
            return new(RecipeOperationStatus.ValidationFailed, ValidationErrors: errors);

        var recipe = await repository.GetByIdAsync(id, ownerUserId, tracked: true, cancellationToken);
        if (recipe is null)
            return new(RecipeOperationStatus.NotFound);

        var title = request.Title.Trim();
        var newSlug = GenerateSlug(title);
        if (newSlug.Length == 0)
            return new(RecipeOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]> { ["title"] = ["Title must contain at least one letter or digit."] });
        if (newSlug != recipe.Slug)
            newSlug = await ResolveSlugAsync(ownerUserId, newSlug, id, cancellationToken);

        recipe.Title = title;
        recipe.Slug = newSlug;
        recipe.DescriptionMarkdown = request.DescriptionMarkdown?.Trim();
        recipe.UpdatedAtUtc = DateTimeOffset.UtcNow;
        recipe.Ingredients.Clear();
        foreach (var ing in MapIngredients(request.Ingredients, id))
            recipe.Ingredients.Add(ing);
        await repository.SaveChangesAsync(cancellationToken);
        return new(RecipeOperationStatus.Success, ToDetailResponse(recipe));
    }

    public async Task<RecipeOperationStatus> DeleteAsync(
        Guid id, Guid ownerUserId, CancellationToken cancellationToken)
    {
        var recipe = await repository.GetByIdAsync(id, ownerUserId, tracked: true, cancellationToken);
        if (recipe is null)
            return RecipeOperationStatus.NotFound;

        repository.Remove(recipe);
        await repository.SaveChangesAsync(cancellationToken);
        return RecipeOperationStatus.Success;
    }

    private static List<RecipeIngredient> MapIngredients(
        IReadOnlyList<RecipeIngredientRequest> requests, Guid recipeId) =>
        requests.Select((r, i) => new RecipeIngredient
        {
            RecipeId = recipeId,
            SortOrder = i,
            Name = r.Name.Trim(),
            Quantity = r.Quantity,
            Unit = r.Unit?.Trim(),
            Note = r.Note?.Trim()
        }).ToList();

    private async Task<string> ResolveSlugAsync(
        Guid ownerUserId, string baseSlug, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (!await repository.SlugExistsAsync(ownerUserId, baseSlug, excludedId, cancellationToken))
            return baseSlug;

        for (var i = 2; i <= 1000; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!await repository.SlugExistsAsync(ownerUserId, candidate, excludedId, cancellationToken))
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

    private static Dictionary<string, string[]> Validate(
        string title, string? descriptionMarkdown, IReadOnlyList<RecipeIngredientRequest> ingredients)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title))
            errors["title"] = ["Title is required."];
        else if (title.Trim().Length > 160)
            errors["title"] = ["Title cannot exceed 160 characters."];

        if (descriptionMarkdown is not null && descriptionMarkdown.Length > 20_000)
            errors["descriptionMarkdown"] = ["Description cannot exceed 20,000 characters."];

        if (ingredients.Count > MaxIngredients)
            errors["ingredients"] = [$"A recipe cannot have more than {MaxIngredients} ingredients."];

        var ingredientErrors = new List<string>();
        for (var i = 0; i < ingredients.Count; i++)
        {
            var ing = ingredients[i];
            if (string.IsNullOrWhiteSpace(ing.Name))
                ingredientErrors.Add($"Ingredient at position {i + 1}: name is required.");
            else if (ing.Name.Trim().Length > 200)
                ingredientErrors.Add($"Ingredient at position {i + 1}: name cannot exceed 200 characters.");

            if (ing.Quantity.HasValue && ing.Quantity.Value <= 0)
                ingredientErrors.Add($"Ingredient at position {i + 1}: quantity must be positive.");

            if (ing.Unit is not null && ing.Unit.Trim().Length > 40)
                ingredientErrors.Add($"Ingredient at position {i + 1}: unit cannot exceed 40 characters.");

            if (ing.Note is not null && ing.Note.Trim().Length > 300)
                ingredientErrors.Add($"Ingredient at position {i + 1}: note cannot exceed 300 characters.");
        }
        if (ingredientErrors.Count > 0)
            errors["ingredients"] = ingredientErrors.ToArray();

        return errors;
    }

    private static RecipeDetailResponse ToDetailResponse(Recipe recipe, IReadOnlyList<RecipeImageResponse>? images = null) =>
        new(recipe.Id, recipe.Title, recipe.Slug, recipe.DescriptionMarkdown,
            recipe.Ingredients
                .OrderBy(i => i.SortOrder)
                .Select(i => new RecipeIngredientResponse(i.Id, i.SortOrder, i.Name, i.Quantity, i.Unit, i.Note))
                .ToList(),
            images ?? [],
            recipe.CreatedAtUtc, recipe.UpdatedAtUtc);

    private async Task<IReadOnlyDictionary<Guid, Guid>> GetFirstImageIdsAsync(
        IReadOnlyList<Guid> recipeIds, CancellationToken cancellationToken)
    {
        if (imageRepository is null || recipeIds.Count == 0)
            return new Dictionary<Guid, Guid>();
        return await imageRepository.GetFirstImageIdsAsync(recipeIds, cancellationToken);
    }

    private static RecipeSummaryResponse ToSummaryResponse(
        Recipe recipe, IReadOnlyDictionary<Guid, Guid>? firstImageIds = null)
    {
        string? firstImageUrl = null;
        if (firstImageIds is not null && firstImageIds.TryGetValue(recipe.Id, out var imageId))
            firstImageUrl = $"/api/recipes/{recipe.Id}/images/{imageId}/content";
        return new(recipe.Id, recipe.Title, recipe.Slug, recipe.Ingredients.Count,
            firstImageUrl, recipe.CreatedAtUtc, recipe.UpdatedAtUtc);
    }

    private static RecipeImageResponse ToImageResponse(RecipeImage i) => new(i.Id, i.RecipeId, i.FileName,
        i.ContentType, i.FileSizeBytes, i.AltText, i.SortOrder,
        $"/api/recipes/{i.RecipeId}/images/{i.Id}/content", i.CreatedAtUtc);
}
