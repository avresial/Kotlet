using Kotlet.Application.Ingredients;
using Kotlet.Application.PreparedMeals;
using Kotlet.Application.Recipes;
using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.MealPlanner;

/// <summary>Adds, previews, relocates and removes individual planned meals.</summary>
public sealed class MealPlanItemService(
    IMealPlanRepository repository,
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IPreparedMealRepository? preparedMealRepository = null)
{
    private readonly MealPlanResponseBuilder _responses =
        new(repository, recipeRepository, ingredientRepository, preparedMealRepository);
    private readonly MealPlanItemValidator _validator =
        new(recipeRepository, ingredientRepository, preparedMealRepository);

    public async Task<MealPlannerOperationResult> AddItemAsync(
        Guid userId, Guid houseId, AddMealPlanItemRequest request, CancellationToken cancellationToken)
    {
        var errors = await _validator.ValidateAddAsync(houseId, request, cancellationToken);
        if (errors.Count > 0)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: errors);
        }

        var slot = MealSlots.Parse(request.Slot);
        var existingCount = (await repository.GetByDateAsync(houseId, request.Date, cancellationToken))
            .Count(i => i.Slot == slot);

        var item = new MealPlanItem
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            UserId = userId,
            Date = request.Date,
            Slot = slot,
            RecipeId = request.RecipeId,
            IngredientId = request.IngredientId,
            PreparedMealId = request.PreparedMealId,
            FreeText = request.FreeText?.Trim(),
            Note = request.Note?.Trim(),
            SortOrder = existingCount,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        repository.Add(item);
        foreach (var addon in request.Addons ?? [])
        {
            repository.Add(new MealPlanItem
            {
                Id = Guid.NewGuid(),
                HouseId = houseId,
                UserId = userId,
                Date = request.Date,
                Slot = slot,
                IngredientId = addon.IngredientId,
                ParentMealPlanItemId = item.Id,
                IngredientQuantity = addon.Quantity,
                IngredientUnit = addon.Unit.Trim(),
                SortOrder = ++existingCount,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            });
        }
        await repository.SaveChangesAsync(cancellationToken);

        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        var response = await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    public async Task<WeeklyMealPlannerOperationResult> AddWeekAsync(
        Guid userId, Guid houseId, AddWeeklyMealPlanRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateWeekAsync(
            houseId, request, requireMeals: false, cancellationToken);
        var errors = validation.Errors;
        if (errors.Count > 0)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: errors);
        }

        var existing = await repository.GetByDateRangeAsync(
            houseId, request.WeekStart, request.WeekStart.AddDays(6), cancellationToken);
        var keys = existing.Select(MealKey).ToHashSet();
        var slotCounts = existing.GroupBy(item => (item.Date, item.Slot))
            .ToDictionary(group => group.Key, group => group.Count());
        var added = new List<MealPlanItem>();
        var skipped = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var meal in request.Meals)
        {
            var slot = MealSlots.Parse(meal.Slot);
            var key = (meal.Date, slot, meal.RecipeId, meal.IngredientId, meal.PreparedMealId, meal.FreeText?.Trim());
            if (!keys.Add(key))
            {
                skipped++;
                continue;
            }

            var slotKey = (meal.Date, slot);
            var item = new MealPlanItem
            {
                Id = Guid.NewGuid(),
                HouseId = houseId,
                UserId = userId,
                Date = meal.Date,
                Slot = slot,
                RecipeId = meal.RecipeId,
                IngredientId = meal.IngredientId,
                PreparedMealId = meal.PreparedMealId,
                FreeText = meal.FreeText?.Trim(),
                Note = meal.Note?.Trim(),
                SortOrder = slotCounts.GetValueOrDefault(slotKey),
                CreatedAt = now,
                UpdatedAt = now
            };
            slotCounts[slotKey] = item.SortOrder + 1;
            repository.Add(item);
            foreach (var addon in meal.Addons ?? [])
            {
                repository.Add(new MealPlanItem
                {
                    Id = Guid.NewGuid(),
                    HouseId = houseId,
                    UserId = userId,
                    Date = meal.Date,
                    Slot = slot,
                    IngredientId = addon.IngredientId,
                    ParentMealPlanItemId = item.Id,
                    IngredientQuantity = addon.Quantity,
                    IngredientUnit = addon.Unit.Trim(),
                    SortOrder = slotCounts[slotKey]++,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            added.Add(item);
        }

        if (added.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        var responses = added
            .Select(item => MealPlanResponseBuilder.ToResponse(
                item,
                userId,
                members,
                MealPlanResponseBuilder.ResolveDisplay(item, validation.Context)))
            .ToList();
        return new(MealPlannerOperationStatus.Success, new(responses, skipped));
    }

    public async Task<MealPlanPreviewResult> PreviewWeekAsync(
        Guid houseId, AddWeeklyMealPlanRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateWeekAsync(
            houseId, request, requireMeals: true, cancellationToken);
        if (validation.Errors.Count > 0)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: validation.Errors);
        }

        var meals = request.Meals
            .OrderBy(meal => meal.Date)
            .ThenBy(meal => MealSlots.Parse(meal.Slot))
            .Select(meal => MealPlanResponseBuilder.ToPreview(meal, validation.Context))
            .ToList();
        return new(
            MealPlannerOperationStatus.Success,
            new(request.WeekStart.ToString("yyyy-MM-dd"), meals));
    }

    /// <summary>
    /// Moves a meal to a different day and/or slot, appending it to the end of the
    /// target slot. Used by the drag-and-drop planner to relocate a planned meal
    /// without recreating it, preserving its people, guests and serving overrides.
    /// </summary>
    public async Task<MealPlannerOperationResult> MoveItemAsync(
        Guid userId, Guid houseId, Guid itemId, MoveMealPlanItemRequest request, CancellationToken cancellationToken)
    {
        if (MealSlots.Error(request.Slot) is { } slotError)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: slotError);
        }

        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null)
        {
            return new(MealPlannerOperationStatus.NotFound);
        }

        var targetSlot = MealSlots.Parse(request.Slot);
        if (item.Date != request.Date || item.Slot != targetSlot)
        {
            var targetCount = (await repository.GetByDateAsync(houseId, request.Date, cancellationToken))
                .Count(i => i.Slot == targetSlot && i.Id != item.Id);
            item.Date = request.Date;
            item.Slot = targetSlot;
            item.SortOrder = targetCount;
            item.Version++;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            foreach (var addon in item.AddonItems)
            {
                addon.Date = request.Date;
                addon.Slot = targetSlot;
                addon.UpdatedAt = item.UpdatedAt;
            }
            await repository.SaveChangesAsync(cancellationToken);
        }

        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        var response = await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    public async Task<MealPlannerOperationStatus> RemoveItemAsync(
        Guid houseId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null)
        {
            return MealPlannerOperationStatus.NotFound;
        }

        repository.Remove(item);
        await repository.SaveChangesAsync(cancellationToken);
        return MealPlannerOperationStatus.Success;
    }

    private static (DateOnly, MealSlot, Guid?, Guid?, Guid?, string?) MealKey(MealPlanItem item) =>
        (item.Date, item.Slot, item.RecipeId, item.IngredientId, item.PreparedMealId, item.FreeText?.Trim());
}
