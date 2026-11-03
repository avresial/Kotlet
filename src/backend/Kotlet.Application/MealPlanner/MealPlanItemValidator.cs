using Kotlet.Application.Ingredients;
using Kotlet.Application.PreparedMeals;
using Kotlet.Application.Recipes;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.PreparedMeals;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.MealPlanner;

/// <summary>Validates that a planned meal references exactly one existing source and valid add-ons.</summary>
internal sealed class MealPlanItemValidator(
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IPreparedMealRepository? preparedMealRepository)
{
    public async Task<Dictionary<string, string[]>> ValidateSourceAsync(
        Guid houseId, MealPlanSourceRequest? source, CancellationToken cancellationToken)
    {
        if (source is null)
        {
            return new() { ["source"] = ["A meal source is required."] };
        }

        var request = new AddMealPlanItemRequest(
            DateOnly.MinValue,
            "dinner",
            source.RecipeId,
            source.IngredientId,
            source.Note,
            source.PreparedMealId,
            source.Addons,
            source.FreeText,
            source.ConfirmUncatalogued);
        return await ValidateAddAsync(houseId, request, cancellationToken, validateSlot: false);
    }

    public async Task<Dictionary<string, string[]>> ValidateAddAsync(
        Guid houseId, AddMealPlanItemRequest request, CancellationToken cancellationToken, bool validateSlot = true)
    {
        var errors = new Dictionary<string, string[]>();

        if (validateSlot && MealSlots.Error(request.Slot) is { } slotError)
        {
            errors["slot"] = slotError["slot"];
        }

        var hasRecipe = request.RecipeId.HasValue;
        var hasIngredient = request.IngredientId.HasValue;
        var hasPreparedMeal = request.PreparedMealId.HasValue;
        var hasFreeText = request.FreeText is not null;

        if ((hasRecipe ? 1 : 0) + (hasIngredient ? 1 : 0) + (hasPreparedMeal ? 1 : 0) + (hasFreeText ? 1 : 0) != 1)
        {
            errors["item"] = ["Exactly one of recipeId, ingredientId, preparedMealId or freeText must be provided."];
        }
        else if (hasFreeText && !request.ConfirmUncatalogued)
        {
            errors["confirmUncatalogued"] = ["Free-text meals require explicit confirmation after recipe lookup."];
        }
        else if (!hasFreeText && request.ConfirmUncatalogued)
        {
            errors["confirmUncatalogued"] = ["confirmUncatalogued can only be used with freeText."];
        }
        else if (!hasPreparedMeal && request.Addons is { Count: > 0 })
        {
            errors["addons"] = ["Add-ons can only be selected for a prepared meal."];
        }
        else if (hasRecipe)
        {
            var recipe = await recipeRepository.GetByIdAsync(request.RecipeId!.Value, houseId, tracked: false, cancellationToken);
            if (recipe is null)
            {
                errors["recipeId"] = ["Recipe not found."];
            }
        }
        else if (hasIngredient)
        {
            var ingredient = await ingredientRepository.GetByIdAsync(request.IngredientId!.Value, tracked: false, cancellationToken);
            if (ingredient is null)
            {
                errors["ingredientId"] = ["Ingredient not found."];
            }
        }
        else if (hasFreeText)
        {
            if (string.IsNullOrWhiteSpace(request.FreeText))
            {
                errors["freeText"] = ["Free text is required."];
            }
            else if (request.FreeText.Trim().Length > 500)
            {
                errors["freeText"] = ["Free text cannot exceed 500 characters."];
            }
        }
        else if (preparedMealRepository is null || await preparedMealRepository.GetAsync(request.PreparedMealId!.Value, houseId, false, cancellationToken) is not { IsArchived: false } meal)
        {
            errors["preparedMealId"] = ["Prepared meal not found or archived."];
        }
        else
        {
            var selected = request.Addons ?? [];
            if (selected.GroupBy(a => a.IngredientId).Any(g => g.Count() > 1))
            {
                errors["addons"] = ["Duplicate add-ons are not allowed."];
            }
            if (meal.Addons.Where(a => a.IsRequired).Any(required => selected.All(a => a.IngredientId != required.IngredientId)))
            {
                errors["addons"] = ["Required add-ons must be selected."];
            }
            foreach (var addon in selected)
            {
                if (addon.Quantity <= 0 || string.IsNullOrWhiteSpace(addon.Unit))
                {
                    errors["addons"] = ["Add-on quantity and unit are required."];
                }
                if (meal.Addons.All(a => a.IngredientId != addon.IngredientId))
                {
                    errors["addons"] = ["Selected add-on is not configured for this prepared meal."];
                }
            }
        }

        return errors;
    }

    public async Task<WeekValidation> ValidateWeekAsync(
        Guid houseId,
        AddWeeklyMealPlanRequest request,
        bool requireMeals,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (requireMeals && request.Meals.Count == 0)
        {
            errors["meals"] = ["Add at least one meal to the weekly plan."];
        }
        else if (request.Meals.Count > 35)
        {
            errors["meals"] = ["A weekly plan cannot contain more than 35 meals."];
        }

        var recipeIds = request.Meals
            .Where(meal => meal.RecipeId.HasValue)
            .Select(meal => meal.RecipeId!.Value)
            .Distinct()
            .ToArray();
        var ingredientIds = request.Meals
            .Where(meal => meal.IngredientId.HasValue)
            .Select(meal => meal.IngredientId!.Value)
            .Distinct()
            .ToArray();
        var preparedMealIds = request.Meals
            .Where(meal => meal.PreparedMealId.HasValue)
            .Select(meal => meal.PreparedMealId!.Value)
            .Distinct()
            .ToArray();

        var recipes = await recipeRepository.GetByIdsAsync(recipeIds, houseId, cancellationToken);
        var ingredients = await ingredientRepository.GetByIdsAsync(ingredientIds, cancellationToken);
        var preparedMeals = preparedMealRepository is null
            ? new Dictionary<Guid, PreparedMeal>()
            : await preparedMealRepository.GetByIdsAsync(preparedMealIds, houseId, cancellationToken);
        var context = new WeekLookupContext(recipes, ingredients, preparedMeals);

        for (var index = 0; index < request.Meals.Count; index++)
        {
            var meal = request.Meals[index];
            if (meal.Date < request.WeekStart || meal.Date > request.WeekStart.AddDays(6))
            {
                errors[$"meals[{index}].date"] = ["Date must be within the seven-day week starting at weekStart."];
            }
            if (MealSlots.Error(meal.Slot) is { } slotError)
            {
                errors[$"meals[{index}].slot"] = slotError["slot"];
            }

            var hasRecipe = meal.RecipeId.HasValue;
            var hasIngredient = meal.IngredientId.HasValue;
            var hasPreparedMeal = meal.PreparedMealId.HasValue;
            var hasFreeText = meal.FreeText is not null;
            if ((hasRecipe ? 1 : 0) + (hasIngredient ? 1 : 0) + (hasPreparedMeal ? 1 : 0) + (hasFreeText ? 1 : 0) != 1)
            {
                errors[$"meals[{index}].item"] =
                    ["Exactly one of recipeId, ingredientId, preparedMealId or freeText must be provided."];
                continue;
            }

            if (hasFreeText && !meal.ConfirmUncatalogued)
            {
                errors[$"meals[{index}].confirmUncatalogued"] =
                    ["Free-text meals require explicit confirmation after recipe lookup."];
                continue;
            }
            if (!hasFreeText && meal.ConfirmUncatalogued)
            {
                errors[$"meals[{index}].confirmUncatalogued"] =
                    ["confirmUncatalogued can only be used with freeText."];
                continue;
            }

            if (!hasPreparedMeal && meal.Addons is { Count: > 0 })
            {
                errors[$"meals[{index}].addons"] = ["Add-ons can only be selected for a prepared meal."];
            }
            else if (hasRecipe && !recipes.ContainsKey(meal.RecipeId!.Value))
            {
                errors[$"meals[{index}].recipeId"] = ["Recipe not found."];
            }
            else if (hasIngredient && !ingredients.ContainsKey(meal.IngredientId!.Value))
            {
                errors[$"meals[{index}].ingredientId"] = ["Ingredient not found."];
            }
            else if (hasFreeText && string.IsNullOrWhiteSpace(meal.FreeText))
            {
                errors[$"meals[{index}].freeText"] = ["Free text is required."];
            }
            else if (hasFreeText && meal.FreeText!.Trim().Length > 500)
            {
                errors[$"meals[{index}].freeText"] = ["Free text cannot exceed 500 characters."];
            }
            else if (hasPreparedMeal)
            {
                ValidatePreparedMeal(index, meal, preparedMeals, errors);
            }
        }

        return new(errors, context);
    }

    private static void ValidatePreparedMeal(
        int index,
        AddMealPlanItemRequest request,
        IReadOnlyDictionary<Guid, PreparedMeal> preparedMeals,
        IDictionary<string, string[]> errors)
    {
        if (!preparedMeals.TryGetValue(request.PreparedMealId!.Value, out var meal) || meal.IsArchived)
        {
            errors[$"meals[{index}].preparedMealId"] = ["Prepared meal not found or archived."];
            return;
        }

        var selected = request.Addons ?? [];
        if (selected.GroupBy(addon => addon.IngredientId).Any(group => group.Count() > 1))
        {
            errors[$"meals[{index}].addons"] = ["Duplicate add-ons are not allowed."];
        }
        if (meal.Addons.Where(addon => addon.IsRequired)
            .Any(required => selected.All(addon => addon.IngredientId != required.IngredientId)))
        {
            errors[$"meals[{index}].addons"] = ["Required add-ons must be selected."];
        }
        foreach (var addon in selected)
        {
            if (addon.Quantity <= 0 || string.IsNullOrWhiteSpace(addon.Unit))
            {
                errors[$"meals[{index}].addons"] = ["Add-on quantity and unit are required."];
            }
            if (meal.Addons.All(configured => configured.IngredientId != addon.IngredientId))
            {
                errors[$"meals[{index}].addons"] = ["Selected add-on is not configured for this prepared meal."];
            }
        }
    }
}

internal sealed record WeekLookupContext(
    IReadOnlyDictionary<Guid, Recipe> Recipes,
    IReadOnlyDictionary<Guid, Ingredient> Ingredients,
    IReadOnlyDictionary<Guid, PreparedMeal> PreparedMeals);

internal sealed record WeekValidation(
    Dictionary<string, string[]> Errors,
    WeekLookupContext Context);
