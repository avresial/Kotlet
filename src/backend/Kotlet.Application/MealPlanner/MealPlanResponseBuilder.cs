using Kotlet.Application.Ingredients;
using Kotlet.Application.PreparedMeals;
using Kotlet.Application.Recipes;
using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.MealPlanner;

/// <summary>Maps meal-plan items to API responses, resolving display names and participant names.</summary>
internal sealed class MealPlanResponseBuilder(
    IMealPlanRepository repository,
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IPreparedMealRepository? preparedMealRepository)
{
    public static MealPlanPreviewItem ToPreview(
        AddMealPlanItemRequest request, WeekLookupContext context)
    {
        if (request.RecipeId is { } recipeId)
        {
            var recipe = context.Recipes[recipeId];
            return new(
                request.Date.ToString("yyyy-MM-dd"),
                request.Slot,
                "recipe",
                recipeId,
                null,
                null,
                recipe.Title,
                recipe.Servings.Value,
                null,
                recipe.Ingredients.OrderBy(item => item.SortOrder)
                    .Select(item => item.Ingredient.Name)
                    .ToList(),
                request.Note?.Trim(),
                $"kotlet://recipes/{recipeId}");
        }

        if (request.IngredientId is { } ingredientId)
        {
            var ingredient = context.Ingredients[ingredientId];
            return new(
                request.Date.ToString("yyyy-MM-dd"),
                request.Slot,
                "ingredient",
                null,
                ingredientId,
                null,
                ingredient.Name,
                null,
                null,
                [ingredient.Name],
                request.Note?.Trim(),
                $"kotlet://ingredients/{ingredientId}");
        }

        if (request.FreeText is { } freeText)
        {
            return new(
                request.Date.ToString("yyyy-MM-dd"),
                request.Slot,
                "free-text",
                null,
                null,
                null,
                freeText.Trim(),
                null,
                null,
                [freeText.Trim()],
                request.Note?.Trim(),
                "kotlet://meal-plans/free-text",
                freeText.Trim());
        }

        var preparedMealId = request.PreparedMealId!.Value;
        var preparedMeal = context.PreparedMeals[preparedMealId];
        var selectedIds = (request.Addons ?? []).Select(addon => addon.IngredientId).ToHashSet();
        return new(
            request.Date.ToString("yyyy-MM-dd"),
            request.Slot,
            "prepared-meal",
            null,
            null,
            preparedMealId,
            preparedMeal.Name,
            preparedMeal.Servings,
            preparedMeal.CaloriesPerServing,
            preparedMeal.Addons
                .Where(addon => selectedIds.Contains(addon.IngredientId))
                .OrderBy(addon => addon.SortOrder)
                .Select(addon => addon.Ingredient?.Name ?? "Unknown ingredient")
                .ToList(),
            request.Note?.Trim(),
            $"kotlet://prepared-meals/{preparedMealId}");
    }

    public static (string DisplayName, string Type) ResolveDisplay(
        MealPlanItem item, WeekLookupContext context)
    {
        if (item.RecipeId is { } recipeId)
        {
            return (context.Recipes[recipeId].Title, "recipe");
        }
        if (item.IngredientId is { } ingredientId)
        {
            return (context.Ingredients[ingredientId].Name, "ingredient");
        }
        if (item.FreeText is { } freeText)
        {
            return (freeText, "free-text");
        }
        return (context.PreparedMeals[item.PreparedMealId!.Value].Name, "prepared-meal");
    }

    public static (string DisplayName, string Type) ResolveDisplay(
        MealPlanItem item,
        IReadOnlyDictionary<Guid, string> recipes,
        IReadOnlyDictionary<Guid, string> ingredients,
        IReadOnlyDictionary<Guid, string> preparedMeals)
    {
        if (item.RecipeId is { } recipeId)
        {
            return (recipes.GetValueOrDefault(recipeId, "Unknown recipe"), "recipe");
        }
        if (item.IngredientId is { } ingredientId)
        {
            return (ingredients.GetValueOrDefault(ingredientId, "Unknown ingredient"), "ingredient");
        }
        if (item.FreeText is { } freeText)
        {
            return (freeText, "free-text");
        }
        if (item.PreparedMealId is { } preparedMealId)
        {
            return (preparedMeals.GetValueOrDefault(preparedMealId, "Unknown prepared meal"), "prepared-meal");
        }
        return ("Unknown meal", "unknown");
    }

    public async Task<Dictionary<Guid, string>> GetMemberNamesAsync(Guid houseId, CancellationToken cancellationToken)
    {
        var members = await repository.GetHouseMembersAsync(houseId, cancellationToken);
        return members.ToDictionary(m => m.UserId, m => m.DisplayName);
    }

    public async Task<MealPlanItemResponse> ToResponseAsync(
        MealPlanItem item, Guid userId, Guid houseId, IReadOnlyDictionary<Guid, string> memberNames, CancellationToken cancellationToken)
    {
        string displayName;
        string type;

        if (item.RecipeId.HasValue)
        {
            var recipe = await recipeRepository.GetByIdAsync(item.RecipeId.Value, houseId, tracked: false, cancellationToken);
            displayName = recipe?.Title ?? "Unknown recipe";
            type = "recipe";
        }
        else if (item.IngredientId.HasValue)
        {
            var ingredient = await ingredientRepository.GetByIdAsync(item.IngredientId!.Value, tracked: false, cancellationToken);
            displayName = ingredient?.Name ?? "Unknown ingredient";
            type = "ingredient";
        }
        else if (item.FreeText is { } freeText)
        {
            displayName = freeText;
            type = "free-text";
        }
        else
        {
            var meal = preparedMealRepository is null ? null : await preparedMealRepository.GetAsync(item.PreparedMealId!.Value, houseId, false, cancellationToken);
            displayName = meal?.Name ?? "Unknown prepared meal";
            type = "prepared-meal";
        }

        return ToResponse(item, userId, memberNames, (displayName, type));
    }

    public static MealPlanItemResponse ToResponse(
        MealPlanItem item,
        Guid userId,
        IReadOnlyDictionary<Guid, string> memberNames,
        (string DisplayName, string Type) display)
    {
        var participants = item.Participants
            .Select(p => new MealParticipantResponse(
                p.UserId,
                memberNames.TryGetValue(p.UserId, out var name) ? name : "Unknown",
                p.UserId == userId,
                p.PortionPercent))
            .OrderByDescending(p => p.IsCurrentUser)
            .ThenBy(p => p.DisplayName)
            .ToList();

        return new MealPlanItemResponse(
            item.Id,
            MealSlots.ToName(item.Slot),
            display.Type,
            item.RecipeId,
            item.IngredientId,
            item.PreparedMealId,
            item.ParentMealPlanItemId,
            item.IngredientQuantity,
            item.IngredientUnit,
            display.DisplayName,
            item.Note,
            item.SortOrder,
            participants,
            item.Guests,
            item.EffectiveServings,
            item.Servings.HasValue,
            item.FreeText,
            item.Version);
    }
}
