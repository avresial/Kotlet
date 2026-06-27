using Kotlet.Application.Ingredients;
using Kotlet.Application.Recipes;
using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.MealPlanner;

public sealed class MealPlannerService(
    IMealPlanRepository repository,
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository)
{
    private static readonly HashSet<string> ValidSlots = ["breakfast", "dinner", "supper"];

    public async Task<DailyMealPlanResponse> GetForDateAsync(
        Guid userId, DateOnly date, CancellationToken cancellationToken)
    {
        var items = await repository.GetByDateAsync(userId, date, cancellationToken);
        var responses = new List<MealPlanItemResponse>();
        foreach (var item in items)
        {
            var response = await ToResponseAsync(item, userId, cancellationToken);
            responses.Add(response);
        }

        return new DailyMealPlanResponse(
            date.ToString("yyyy-MM-dd"),
            new Dictionary<string, IReadOnlyList<MealPlanItemResponse>>
            {
                ["breakfast"] = responses.Where(r => r.Slot == "breakfast").OrderBy(r => r.SortOrder).ToList(),
                ["dinner"] = responses.Where(r => r.Slot == "dinner").OrderBy(r => r.SortOrder).ToList(),
                ["supper"] = responses.Where(r => r.Slot == "supper").OrderBy(r => r.SortOrder).ToList()
            });
    }

    public async Task<MealPlannerOperationResult> AddItemAsync(
        Guid userId, AddMealPlanItemRequest request, CancellationToken cancellationToken)
    {
        var errors = await ValidateAddAsync(userId, request, cancellationToken);
        if (errors.Count > 0)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: errors);

        var slot = ParseSlot(request.Slot);
        var existingCount = (await repository.GetByDateAsync(userId, request.Date, cancellationToken))
            .Count(i => i.Slot == slot);

        var item = new MealPlanItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = request.Date,
            Slot = slot,
            RecipeId = request.RecipeId,
            IngredientId = request.IngredientId,
            Note = request.Note?.Trim(),
            SortOrder = existingCount,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        repository.Add(item);
        await repository.SaveChangesAsync(cancellationToken);

        var response = await ToResponseAsync(item, userId, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    public async Task<MealPlannerOperationStatus> RemoveItemAsync(
        Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(itemId, userId, cancellationToken);
        if (item is null) return MealPlannerOperationStatus.NotFound;

        repository.Remove(item);
        await repository.SaveChangesAsync(cancellationToken);
        return MealPlannerOperationStatus.Success;
    }

    private async Task<Dictionary<string, string[]>> ValidateAddAsync(
        Guid userId, AddMealPlanItemRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Slot) || !ValidSlots.Contains(request.Slot.ToLower()))
            errors["slot"] = [$"Slot must be one of: {string.Join(", ", ValidSlots)}."];

        var hasRecipe = request.RecipeId.HasValue;
        var hasIngredient = request.IngredientId.HasValue;

        if (!hasRecipe && !hasIngredient)
            errors["item"] = ["Either recipeId or ingredientId must be provided."];
        else if (hasRecipe && hasIngredient)
            errors["item"] = ["Only one of recipeId or ingredientId may be provided."];
        else if (hasRecipe)
        {
            var recipe = await recipeRepository.GetByIdAsync(request.RecipeId!.Value, userId, tracked: false, cancellationToken);
            if (recipe is null) errors["recipeId"] = ["Recipe not found."];
        }
        else
        {
            var ingredient = await ingredientRepository.GetByIdAsync(request.IngredientId!.Value, tracked: false, cancellationToken);
            if (ingredient is null) errors["ingredientId"] = ["Ingredient not found."];
        }

        return errors;
    }

    private async Task<MealPlanItemResponse> ToResponseAsync(
        MealPlanItem item, Guid userId, CancellationToken cancellationToken)
    {
        string displayName;
        string type;

        if (item.RecipeId.HasValue)
        {
            var recipe = await recipeRepository.GetByIdAsync(item.RecipeId.Value, userId, tracked: false, cancellationToken);
            displayName = recipe?.Title ?? "Unknown recipe";
            type = "recipe";
        }
        else
        {
            var ingredient = await ingredientRepository.GetByIdAsync(item.IngredientId!.Value, tracked: false, cancellationToken);
            displayName = ingredient?.Name ?? "Unknown ingredient";
            type = "ingredient";
        }

        return new MealPlanItemResponse(
            item.Id,
            SlotToString(item.Slot),
            type,
            item.RecipeId,
            item.IngredientId,
            displayName,
            item.Note,
            item.SortOrder);
    }

    private static MealSlot ParseSlot(string slot) => slot.ToLower() switch
    {
        "breakfast" => MealSlot.Breakfast,
        "dinner" => MealSlot.Dinner,
        "supper" => MealSlot.Supper,
        _ => throw new InvalidOperationException($"Invalid slot: {slot}")
    };

    private static string SlotToString(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "breakfast",
        MealSlot.Dinner => "dinner",
        MealSlot.Supper => "supper",
        _ => throw new InvalidOperationException($"Unknown slot: {slot}")
    };
}
