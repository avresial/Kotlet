using Kotlet.Application.Ingredients;
using Kotlet.Application.Recipes;
using Kotlet.Application.PreparedMeals;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.PreparedMeals;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.MealPlanner;

public sealed class MealPlannerService(
    IMealPlanRepository repository,
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IPreparedMealRepository? preparedMealRepository = null)
{
    private static readonly HashSet<string> ValidSlots = ["breakfast", "second-breakfast", "dinner", "snack", "supper"];
    private const int MaxServings = 99;
    private const int MaxGuests = 99;

    public async Task<DailyMealPlanResponse> GetForDateAsync(
        Guid userId, Guid houseId, DateOnly date, CancellationToken cancellationToken) =>
        (await GetForRangeAsync(userId, houseId, date, 1, cancellationToken))[0];

    public async Task<IReadOnlyList<DailyMealPlanResponse>> GetForRangeAsync(
        Guid userId, Guid houseId, DateOnly from, int days, CancellationToken cancellationToken)
    {
        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var items = await repository.GetByDateRangeAsync(houseId, from, from.AddDays(days - 1), cancellationToken);
        var recipes = items.Any(item => item.RecipeId.HasValue)
            ? (await recipeRepository.GetAllForDuplicateCheckAsync(houseId, cancellationToken))
                .ToDictionary(recipe => recipe.Id, recipe => recipe.Title)
            : [];
        var ingredientIds = items.Where(item => item.IngredientId.HasValue)
            .Select(item => item.IngredientId!.Value).Distinct().ToArray();
        var ingredients = ingredientIds.Length == 0
            ? []
            : (await ingredientRepository.GetByIdsAsync(ingredientIds, cancellationToken))
                .ToDictionary(entry => entry.Key, entry => entry.Value.Name);
        var preparedMeals = items.Any(item => item.PreparedMealId.HasValue) && preparedMealRepository is not null
            ? (await preparedMealRepository.ListAsync(houseId, includeArchived: true, cancellationToken))
                .ToDictionary(meal => meal.Id, meal => meal.Name)
            : [];

        return Enumerable.Range(0, days).Select(offset =>
        {
            var date = from.AddDays(offset);
            var responses = items.Where(item => item.Date == date).Select(item =>
                item.RecipeId is { } recipeId
                    ? ToResponse(item, userId, members, recipes.GetValueOrDefault(recipeId, "Unknown recipe"), "recipe")
                    : item.IngredientId is { } ingredientId
                        ? ToResponse(item, userId, members, ingredients.GetValueOrDefault(ingredientId, "Unknown ingredient"), "ingredient")
                        : item.FreeText is { } freeText
                            ? ToResponse(item, userId, members, freeText, "free-text")
                            : ToResponse(item, userId, members,
                                item.PreparedMealId is { } preparedMealId
                                    ? preparedMeals.GetValueOrDefault(preparedMealId, "Unknown prepared meal")
                                    : "Unknown meal",
                                item.PreparedMealId.HasValue ? "prepared-meal" : "unknown"))
                .ToList();
            return new DailyMealPlanResponse(
                date.ToString("yyyy-MM-dd"),
            new Dictionary<string, IReadOnlyList<MealPlanItemResponse>>
            {
                ["breakfast"] = responses.Where(r => r.Slot == "breakfast").OrderBy(r => r.SortOrder).ToList(),
                ["second-breakfast"] = responses.Where(r => r.Slot == "second-breakfast").OrderBy(r => r.SortOrder).ToList(),
                ["dinner"] = responses.Where(r => r.Slot == "dinner").OrderBy(r => r.SortOrder).ToList(),
                ["snack"] = responses.Where(r => r.Slot == "snack").OrderBy(r => r.SortOrder).ToList(),
                ["supper"] = responses.Where(r => r.Slot == "supper").OrderBy(r => r.SortOrder).ToList()
            });
        }).ToList();
    }

    public async Task<IReadOnlyList<MealHouseMember>> GetHouseMembersAsync(
        Guid houseId, CancellationToken cancellationToken) =>
        await repository.GetHouseMembersAsync(houseId, cancellationToken);

    public async Task<IReadOnlyList<MealPlanOverviewDay>> GetOverviewAsync(
        Guid userId, Guid houseId, DateOnly from, int days, CancellationToken cancellationToken)
    {
        var plans = await GetForRangeAsync(userId, houseId, from, days, cancellationToken);
        return plans
            .Select(plan =>
            {
                var meals = plan.Meals
                    .Where(pair => pair.Value.Count > 0)
                    .ToDictionary(
                        pair => pair.Key,
                        pair => (IReadOnlyList<string>)pair.Value.Select(item => item.DisplayName).ToList());
                return new MealPlanOverviewDay(plan.Date, meals.Keys.Order().ToList(), meals);
            })
            .ToList();
    }

    public async Task<MealPlannerOperationResult> AddItemAsync(
        Guid userId, Guid houseId, AddMealPlanItemRequest request, CancellationToken cancellationToken)
    {
        var errors = await ValidateAddAsync(houseId, request, cancellationToken);
        if (errors.Count > 0)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: errors);

        var slot = ParseSlot(request.Slot);
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
        await repository.SaveChangesAsync(cancellationToken);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    public async Task<WeeklyMealPlannerOperationResult> AddWeekAsync(
        Guid userId, Guid houseId, AddWeeklyMealPlanRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateWeekAsync(
            houseId, request, requireMeals: false, cancellationToken);
        var errors = validation.Errors;
        if (errors.Count > 0)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: errors);

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
            var slot = ParseSlot(meal.Slot);
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
            await repository.SaveChangesAsync(cancellationToken);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var responses = added
            .Select(item => ToResponse(
                item,
                userId,
                members,
                ResolveDisplay(item, validation.Context)))
            .ToList();
        return new(MealPlannerOperationStatus.Success, new(responses, skipped));
    }

    public async Task<MealPlanPreviewResult> PreviewWeekAsync(
        Guid houseId, AddWeeklyMealPlanRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateWeekAsync(
            houseId, request, requireMeals: true, cancellationToken);
        if (validation.Errors.Count > 0)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: validation.Errors);

        var meals = request.Meals
            .OrderBy(meal => meal.Date)
            .ThenBy(meal => ParseSlot(meal.Slot))
            .Select(meal => ToPreview(meal, validation.Context))
            .ToList();
        return new(
            MealPlannerOperationStatus.Success,
            new(request.WeekStart.ToString("yyyy-MM-dd"), meals));
    }

    public async Task<CopyMealPlanDayResult> CopyDayAsync(
        Guid userId, Guid houseId, CopyMealPlanDayRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceDate == request.TargetDate)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors:
                new Dictionary<string, string[]> { ["targetDate"] = ["Target date must differ from source date."] });

        var source = await repository.GetByDateAsync(houseId, request.SourceDate, cancellationToken);
        if (source.Count == 0) return new(MealPlannerOperationStatus.NotFound);
        if ((await repository.GetByDateAsync(houseId, request.TargetDate, cancellationToken)).Count > 0)
            return new(MealPlannerOperationStatus.Conflict);

        AddCopies(source, userId, houseId, _ => request.TargetDate);

        await repository.SaveChangesAsync(cancellationToken);
        return new(MealPlannerOperationStatus.Success,
            await GetForDateAsync(userId, houseId, request.TargetDate, cancellationToken));
    }

    public async Task<CopyMealPlanWeekResult> CopyWeekAsync(
        Guid userId, Guid houseId, CopyMealPlanWeekRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceWeekStart.DayOfWeek != DayOfWeek.Monday || request.TargetWeekStart.DayOfWeek != DayOfWeek.Monday)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors:
                new Dictionary<string, string[]> { ["weekStart"] = ["Source and target weeks must start on Monday."] });
        if (request.SourceWeekStart == request.TargetWeekStart)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors:
                new Dictionary<string, string[]> { ["targetWeekStart"] = ["Target week must differ from source week."] });

        var source = await repository.GetByDateRangeAsync(houseId, request.SourceWeekStart, request.SourceWeekStart.AddDays(6), cancellationToken);
        if (source.Count == 0) return new(MealPlannerOperationStatus.NotFound);
        if ((await repository.GetByDateRangeAsync(houseId, request.TargetWeekStart, request.TargetWeekStart.AddDays(6), cancellationToken)).Count > 0)
            return new(MealPlannerOperationStatus.Conflict);

        var offset = request.TargetWeekStart.DayNumber - request.SourceWeekStart.DayNumber;
        AddCopies(source, userId, houseId, original => original.Date.AddDays(offset));

        await repository.SaveChangesAsync(cancellationToken);
        return new(MealPlannerOperationStatus.Success, source.Count);
    }

    /// <summary>
    /// Moves a meal to a different day and/or slot, appending it to the end of the
    /// target slot. Used by the drag-and-drop planner to relocate a planned meal
    /// without recreating it, preserving its people, guests and serving overrides.
    /// </summary>
    public async Task<MealPlannerOperationResult> MoveItemAsync(
        Guid userId, Guid houseId, Guid itemId, MoveMealPlanItemRequest request, CancellationToken cancellationToken)
    {
        if (SlotError(request.Slot) is { } slotError)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: slotError);

        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null) return new(MealPlannerOperationStatus.NotFound);

        var targetSlot = ParseSlot(request.Slot);
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

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    public async Task<MealPlanMutationResponse> ReplaceAsync(
        Guid userId, Guid houseId, ReplaceMealPlanRequest request, CancellationToken cancellationToken)
    {
        var sourceErrors = await ValidateSourceAsync(houseId, request.Source, cancellationToken);
        if (sourceErrors.Count > 0)
            return ValidationMutation(sourceErrors);

        MealPlanItem? item;
        if (request.MealId is { } mealId)
        {
            item = await repository.GetByIdAsync(mealId, houseId, cancellationToken);
            if (item is null) return new("NotFound");
        }
        else
        {
            if (!request.Date.HasValue)
                return ValidationMutation(new Dictionary<string, string[]> { ["date"] = ["Date is required when mealId is omitted."] });
            if (SlotError(request.Slot) is { } slotError)
                return ValidationMutation(slotError);

            var candidates = await GetSlotItemsAsync(houseId, request.Date.Value, ParseSlot(request.Slot!), cancellationToken);
            if (candidates.Count > 1)
                return await AmbiguousMutationAsync(userId, houseId, candidates, cancellationToken);
            var candidate = candidates.SingleOrDefault();
            item = candidate is null
                ? null
                : await repository.GetByIdAsync(candidate.Id, houseId, cancellationToken);
            if (item is null)
            {
                if (!request.CreateIfMissing)
                    return new("NotFound");

                var now = DateTimeOffset.UtcNow;
                item = NewItem(userId, houseId, request.Date.Value, ParseSlot(request.Slot!), candidates.Count, request.Source, now);
                SetMutationKey(item, request.IdempotencyKey);
                repository.Add(item);
                AddSelectedAddons(item, request.Source.Addons, now);
                await repository.SaveChangesAsync(cancellationToken);
                return new("Success", await ToResponseAsync(item, userId, houseId,
                    await GetMemberNamesAsync(houseId, cancellationToken), cancellationToken));
            }
        }

        if (item.ParentMealPlanItemId.HasValue)
            return ValidationMutation(new Dictionary<string, string[]> { ["mealId"] = ["Meal-plan add-ons cannot be replaced directly."] });
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
            string.Equals(item.LastMutationKey, request.IdempotencyKey.Trim(), StringComparison.Ordinal))
            return await SuccessMutationAsync(userId, houseId, item, cancellationToken);
        if (request.ExpectedVersion is { } expectedVersion && item.Version != expectedVersion)
            return VersionConflict(item.Version);
        if (SourceMatches(item, request.Source))
            return new("Success", await ToResponseAsync(item, userId, houseId,
                await GetMemberNamesAsync(houseId, cancellationToken), cancellationToken));

        foreach (var addon in item.AddonItems.ToList())
            repository.Remove(addon);
        item.AddonItems.Clear();
        ApplySource(item, request.Source);
        item.Version++;
        SetMutationKey(item, request.IdempotencyKey);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        AddSelectedAddons(item, request.Source.Addons, item.UpdatedAt);
        await repository.SaveChangesAsync(cancellationToken);

        return new("Success", await ToResponseAsync(item, userId, houseId,
            await GetMemberNamesAsync(houseId, cancellationToken), cancellationToken));
    }

    public async Task<MealPlanMutationResponse> MoveAsync(
        Guid userId, Guid houseId, MoveMealPlanMutationRequest request, CancellationToken cancellationToken)
    {
        if (SlotError(request.Slot) is { } slotError)
            return ValidationMutation(slotError);
        var targetSlot = ParseSlot(request.Slot);
        var behavior = (request.DestinationBehavior ?? "reject").Trim().ToLowerInvariant();
        if (behavior is not ("reject" or "replace" or "swap"))
            return ValidationMutation(new Dictionary<string, string[]> { ["destinationBehavior"] = ["Destination behavior must be reject, replace, or swap."] });
        var mutationKey = request.IdempotencyKey?.Trim();

        var item = await repository.GetByIdAsync(request.MealId, houseId, cancellationToken);
        if (item is null) return new("NotFound");
        if (item.ParentMealPlanItemId.HasValue)
            return ValidationMutation(new Dictionary<string, string[]> { ["mealId"] = ["Meal-plan add-ons cannot be moved directly."] });
        if (!string.IsNullOrWhiteSpace(mutationKey) &&
            string.Equals(item.LastMutationKey, mutationKey, StringComparison.Ordinal))
        {
            if (behavior == "swap")
            {
                var pairedItem = (await repository.GetByDateRangeAsync(
                        houseId, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken))
                    .FirstOrDefault(candidate => candidate.Id != item.Id &&
                        candidate.ParentMealPlanItemId is null &&
                        string.Equals(candidate.LastMutationKey, mutationKey, StringComparison.Ordinal));
                if (pairedItem is not null)
                    return await SuccessPairMutationAsync(userId, houseId, item, pairedItem, cancellationToken);
            }
            return await SuccessMutationAsync(userId, houseId, item, cancellationToken);
        }
        if (request.ExpectedVersion is { } expectedVersion && item.Version != expectedVersion)
            return VersionConflict(item.Version);

        if (item.Date == request.Date && item.Slot == targetSlot)
            return await SuccessMutationAsync(userId, houseId, item, cancellationToken);

        var candidates = (await GetSlotItemsAsync(houseId, request.Date, targetSlot, cancellationToken))
            .Where(candidate => candidate.Id != item.Id)
            .ToList();

        if (behavior == "swap")
        {
            if (candidates.Count != 1)
                return await AmbiguousDestinationAsync(userId, houseId, candidates, request.Date, request.Slot, cancellationToken);

            var destination = await repository.GetByIdAsync(candidates[0].Id, houseId, cancellationToken);
            if (destination is null) return new("NotFound");
            if (request.DestinationExpectedVersion is { } destinationVersion && destination.Version != destinationVersion)
                return VersionConflict(destination.Version);

            var originalDate = item.Date;
            var originalSlot = item.Slot;
            var originalSortOrder = item.SortOrder;
            item.Date = destination.Date;
            item.Slot = destination.Slot;
            item.SortOrder = destination.SortOrder;
            destination.Date = originalDate;
            destination.Slot = originalSlot;
            destination.SortOrder = originalSortOrder;
            UpdateAddonLocation(item);
            UpdateAddonLocation(destination);
            item.Version++;
            SetMutationKey(item, mutationKey);
            SetMutationKey(destination, mutationKey);
            destination.Version++;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            destination.UpdatedAt = item.UpdatedAt;
            await repository.SaveChangesAsync(cancellationToken);
            var members = await GetMemberNamesAsync(houseId, cancellationToken);
            return new("Success", Items: [
                await ToResponseAsync(item, userId, houseId, members, cancellationToken),
                await ToResponseAsync(destination, userId, houseId, members, cancellationToken)
            ]);
        }

        if (candidates.Count > 0)
        {
            if (behavior == "reject" || candidates.Count > 1)
                return await AmbiguousDestinationAsync(userId, houseId, candidates, request.Date, request.Slot, cancellationToken);

            var destination = await repository.GetByIdAsync(candidates[0].Id, houseId, cancellationToken);
            if (destination is null) return new("NotFound");
            if (request.DestinationExpectedVersion is { } destinationVersion && destination.Version != destinationVersion)
                return VersionConflict(destination.Version);
            RemoveWithAddons(destination);
        }

        item.Date = request.Date;
        item.Slot = targetSlot;
        item.SortOrder = 0;
        item.Version++;
        SetMutationKey(item, mutationKey);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        UpdateAddonLocation(item);
        await repository.SaveChangesAsync(cancellationToken);
        return await SuccessMutationAsync(userId, houseId, item, cancellationToken);
    }

    public async Task<MealPlanMutationResponse> SwapAsync(
        Guid userId, Guid houseId, SwapMealPlanRequest request, CancellationToken cancellationToken)
    {
        if (request.FirstMealId == request.SecondMealId)
            return ValidationMutation(new Dictionary<string, string[]> { ["mealId"] = ["The two meals must be different."] });

        var first = await repository.GetByIdAsync(request.FirstMealId, houseId, cancellationToken);
        var second = await repository.GetByIdAsync(request.SecondMealId, houseId, cancellationToken);
        if (first is null || second is null) return new("NotFound");
        if (first.ParentMealPlanItemId.HasValue || second.ParentMealPlanItemId.HasValue)
            return ValidationMutation(new Dictionary<string, string[]> { ["mealId"] = ["Meal-plan add-ons cannot be swapped directly."] });
        var mutationKey = request.IdempotencyKey?.Trim();
        if (!string.IsNullOrWhiteSpace(mutationKey) &&
            string.Equals(first.LastMutationKey, mutationKey, StringComparison.Ordinal) &&
            string.Equals(second.LastMutationKey, mutationKey, StringComparison.Ordinal))
            return await SuccessPairMutationAsync(userId, houseId, first, second, cancellationToken);
        if (request.FirstExpectedVersion is { } firstVersion && first.Version != firstVersion)
            return VersionConflict(first.Version);
        if (request.SecondExpectedVersion is { } secondVersion && second.Version != secondVersion)
            return VersionConflict(second.Version);
        if (first.Date == second.Date && first.Slot == second.Slot)
            return await SuccessPairMutationAsync(userId, houseId, first, second, cancellationToken);

        (first.Date, second.Date) = (second.Date, first.Date);
        (first.Slot, second.Slot) = (second.Slot, first.Slot);
        (first.SortOrder, second.SortOrder) = (second.SortOrder, first.SortOrder);
        UpdateAddonLocation(first);
        UpdateAddonLocation(second);
        first.Version++;
        second.Version++;
        SetMutationKey(first, mutationKey);
        SetMutationKey(second, mutationKey);
        first.UpdatedAt = DateTimeOffset.UtcNow;
        second.UpdatedAt = first.UpdatedAt;
        await repository.SaveChangesAsync(cancellationToken);
        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        return new("Success", Items: [
            await ToResponseAsync(first, userId, houseId, members, cancellationToken),
            await ToResponseAsync(second, userId, houseId, members, cancellationToken)
        ]);
    }

    public async Task<MealPlanMutationResponse> ClearSlotAsync(
        Guid userId, Guid houseId, ClearMealPlanSlotRequest request, CancellationToken cancellationToken)
    {
        if (SlotError(request.Slot) is { } slotError)
            return ValidationMutation(slotError);

        var candidates = await GetSlotItemsAsync(houseId, request.Date, ParseSlot(request.Slot), cancellationToken);
        if (candidates.Count == 0)
            return new("Success", Items: [], Message: "The meal-plan slot is already empty.");

        var tracked = new List<MealPlanItem>();
        foreach (var candidate in candidates)
            if (await repository.GetByIdAsync(candidate.Id, houseId, cancellationToken) is { } item)
                tracked.Add(item);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var responses = new List<MealPlanItemResponse>();
        foreach (var item in tracked)
        {
            responses.Add(await ToResponseAsync(item, userId, houseId, members, cancellationToken));
            RemoveWithAddons(item);
        }
        await repository.SaveChangesAsync(cancellationToken);
        return new("Success", Items: responses);
    }

    public async Task<MealPlanRecommendationResponse> RecommendReplacementAsync(
        Guid houseId, DateOnly date, string slot, CancellationToken cancellationToken)
    {
        if (SlotError(slot) is not null)
            throw new ArgumentException("Invalid meal-plan slot.", nameof(slot));

        var candidates = await GetSlotItemsAsync(houseId, date, ParseSlot(slot), cancellationToken);
        var recommendations = (await recipeRepository.GetAllForDuplicateCheckAsync(houseId, cancellationToken))
            .Take(5)
            .Select(recipe => new MealPlanRecommendation(
                "recipe", recipe.Id, null, null, null, recipe.Title,
                "Household recipe; options are not ranked by preferences.", $"kotlet://recipes/{recipe.Id}"))
            .ToList();
        if (preparedMealRepository is not null && recommendations.Count < 5)
            recommendations.AddRange((await preparedMealRepository.ListAsync(houseId, includeArchived: false, cancellationToken))
                .Take(5 - recommendations.Count)
                .Select(meal => new MealPlanRecommendation(
                    "prepared-meal", null, null, meal.Id, null, meal.Name,
                    "Prepared household meal; options are not ranked by preferences.",
                    $"kotlet://prepared-meals/{meal.Id}")));

        return new(date, slot.ToLowerInvariant(), recommendations, candidates.Count == 0);
    }

    public Task<MealPlanMutationResponse> ApplyReplacementAsync(
        Guid userId, Guid houseId, ApplyMealPlanReplacementRequest request, CancellationToken cancellationToken) =>
        ReplaceAsync(userId, houseId,
            new ReplaceMealPlanRequest(request.MealId, null, null, request.Source, request.ExpectedVersion, request.IdempotencyKey),
            cancellationToken);

    public async Task<MealPlannerOperationStatus> RemoveItemAsync(
        Guid houseId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null) return MealPlannerOperationStatus.NotFound;

        repository.Remove(item);
        await repository.SaveChangesAsync(cancellationToken);
        return MealPlannerOperationStatus.Success;
    }

    /// <summary>
    /// Replaces the set of people assigned to a meal. Passing the full list of house
    /// member ids implements the "add whole house" action; passing a subset removes the
    /// omitted members. All ids must belong to the current user's house.
    /// </summary>
    public async Task<MealPlannerOperationResult> SetParticipantsAsync(
        Guid userId, Guid houseId, Guid itemId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null) return new(MealPlannerOperationStatus.NotFound);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var desired = userIds.Distinct().ToHashSet();

        var unknown = desired.Where(id => !members.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]>
            {
                ["userIds"] = ["One or more selected people are not members of your house."]
            });

        foreach (var participant in item.Participants.Where(p => !desired.Contains(p.UserId)).ToList())
            item.Participants.Remove(participant);

        var current = item.Participants.Select(p => p.UserId).ToHashSet();
        foreach (var id in desired.Where(id => !current.Contains(id)))
            item.Participants.Add(new MealPlanItemParticipant { MealPlanItemId = item.Id, UserId = id });

        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await repository.SaveChangesAsync(cancellationToken);

        var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    /// <summary>
    /// Adds people to a meal without removing anyone already assigned. Ids that are already
    /// participants are ignored, so the call is idempotent. All ids must belong to the current
    /// user's house. Use <see cref="SetParticipantsAsync"/> when the full set should be replaced.
    /// </summary>
    public async Task<MealPlannerOperationResult> AddParticipantsAsync(
        Guid userId, Guid houseId, Guid itemId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null) return new(MealPlannerOperationStatus.NotFound);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var requested = userIds.Distinct().ToList();

        var unknown = requested.Where(id => !members.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]>
            {
                ["userIds"] = ["One or more selected people are not members of your house."]
            });

        var current = item.Participants.Select(p => p.UserId).ToHashSet();
        foreach (var id in requested.Where(id => !current.Contains(id)))
            item.Participants.Add(new MealPlanItemParticipant { MealPlanItemId = item.Id, UserId = id });

        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await repository.SaveChangesAsync(cancellationToken);

        var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    public async Task<MealPlannerOperationResult> SetParticipantPortionAsync(
        Guid userId, Guid houseId, Guid itemId, Guid participantUserId, int portionPercent,
        CancellationToken cancellationToken)
    {
        if (portionPercent is < MealPlanItemParticipant.MinPortionPercent or > MealPlanItemParticipant.MaxPortionPercent)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]>
            {
                ["portionPercent"] = ["Portion percent must be between 50 and 150."]
            });

        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null) return new(MealPlannerOperationStatus.NotFound);

        var participant = item.Participants.SingleOrDefault(value => value.UserId == participantUserId);
        if (participant is null) return new(MealPlannerOperationStatus.NotFound);

        participant.PortionPercent = portionPercent;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await repository.SaveChangesAsync(cancellationToken);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        return new(MealPlannerOperationStatus.Success,
            await ToResponseAsync(item, userId, houseId, members, cancellationToken));
    }

    /// <summary>
    /// Sets an explicit serving count for a meal, or clears it (null) so the count is
    /// derived from the number of assigned participants.
    /// </summary>
    public async Task<MealPlannerOperationResult> SetServingsAsync(
        Guid userId, Guid houseId, Guid itemId, int? servings, CancellationToken cancellationToken)
    {
        if (servings is < 0 or > MaxServings)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]>
            {
                ["servings"] = [$"Servings must be between 0 and {MaxServings}."]
            });

        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null) return new(MealPlannerOperationStatus.NotFound);

        item.Servings = servings;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await repository.SaveChangesAsync(cancellationToken);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    /// <summary>
    /// Sets the number of extra guests joining a meal. Guests add to the headcount
    /// used to derive the serving count when no explicit override is set.
    /// </summary>
    public async Task<MealPlannerOperationResult> SetGuestsAsync(
        Guid userId, Guid houseId, Guid itemId, int guests, CancellationToken cancellationToken)
    {
        if (guests is < 0 or > MaxGuests)
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]>
            {
                ["guests"] = [$"Guests must be between 0 and {MaxGuests}."]
            });

        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null) return new(MealPlannerOperationStatus.NotFound);

        item.Guests = guests;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await repository.SaveChangesAsync(cancellationToken);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    private async Task<Dictionary<string, string[]>> ValidateSourceAsync(
        Guid houseId, MealPlanSourceRequest? source, CancellationToken cancellationToken)
    {
        if (source is null)
            return new() { ["source"] = ["A meal source is required."] };

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

    private async Task<List<MealPlanItem>> GetSlotItemsAsync(
        Guid houseId, DateOnly date, MealSlot slot, CancellationToken cancellationToken) =>
        (await repository.GetByDateAsync(houseId, date, cancellationToken))
            .Where(item => item.ParentMealPlanItemId is null && item.Slot == slot)
            .OrderBy(item => item.SortOrder)
            .ToList();

    private static MealPlanItem NewItem(
        Guid userId, Guid houseId, DateOnly date, MealSlot slot, int sortOrder,
        MealPlanSourceRequest source, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            UserId = userId,
            Date = date,
            Slot = slot,
            RecipeId = source.RecipeId,
            IngredientId = source.IngredientId,
            PreparedMealId = source.PreparedMealId,
            FreeText = source.FreeText?.Trim(),
            Note = source.Note?.Trim(),
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static void ApplySource(MealPlanItem item, MealPlanSourceRequest source)
    {
        item.RecipeId = source.RecipeId;
        item.IngredientId = source.IngredientId;
        item.PreparedMealId = source.PreparedMealId;
        item.FreeText = source.FreeText?.Trim();
        item.Note = source.Note?.Trim();
        item.IngredientQuantity = null;
        item.IngredientUnit = null;
    }

    private void AddSelectedAddons(
        MealPlanItem item, IReadOnlyList<SelectedPreparedMealAddon>? addons, DateTimeOffset now)
    {
        var sortOrder = item.SortOrder + 1;
        foreach (var addon in addons ?? [])
        {
            var child = new MealPlanItem
            {
                Id = Guid.NewGuid(),
                HouseId = item.HouseId,
                UserId = item.UserId,
                Date = item.Date,
                Slot = item.Slot,
                IngredientId = addon.IngredientId,
                ParentMealPlanItemId = item.Id,
                IngredientQuantity = addon.Quantity,
                IngredientUnit = addon.Unit.Trim(),
                SortOrder = sortOrder++,
                CreatedAt = now,
                UpdatedAt = now
            };
            item.AddonItems.Add(child);
            repository.Add(child);
        }
    }

    private static void UpdateAddonLocation(MealPlanItem item)
    {
        foreach (var addon in item.AddonItems)
        {
            addon.Date = item.Date;
            addon.Slot = item.Slot;
            addon.UpdatedAt = item.UpdatedAt;
        }
    }

    private void RemoveWithAddons(MealPlanItem item)
    {
        foreach (var addon in item.AddonItems.ToList())
            repository.Remove(addon);
        item.AddonItems.Clear();
        repository.Remove(item);
    }

    private static bool SourceMatches(MealPlanItem item, MealPlanSourceRequest source)
    {
        if (item.RecipeId != source.RecipeId || item.IngredientId != source.IngredientId ||
            item.PreparedMealId != source.PreparedMealId ||
            !string.Equals(item.FreeText, source.FreeText?.Trim(), StringComparison.Ordinal) ||
            !string.Equals(item.Note, source.Note?.Trim(), StringComparison.Ordinal))
            return false;

        var requested = (source.Addons ?? []).OrderBy(addon => addon.IngredientId).ToList();
        var existing = item.AddonItems.OrderBy(addon => addon.IngredientId).ToList();
        return requested.Count == existing.Count && requested.Zip(existing).All(pair =>
            pair.First.IngredientId == pair.Second.IngredientId &&
            pair.First.Quantity == pair.Second.IngredientQuantity &&
            string.Equals(pair.First.Unit.Trim(), pair.Second.IngredientUnit, StringComparison.Ordinal));
    }

    private async Task<MealPlanMutationResponse> SuccessMutationAsync(
        Guid userId, Guid houseId, MealPlanItem item, CancellationToken cancellationToken)
    {
        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        return new("Success", await ToResponseAsync(item, userId, houseId, members, cancellationToken));
    }

    private async Task<MealPlanMutationResponse> SuccessPairMutationAsync(
        Guid userId, Guid houseId, MealPlanItem first, MealPlanItem second, CancellationToken cancellationToken)
    {
        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        return new("Success", Items: [
            await ToResponseAsync(first, userId, houseId, members, cancellationToken),
            await ToResponseAsync(second, userId, houseId, members, cancellationToken)
        ]);
    }

    private async Task<MealPlanMutationResponse> AmbiguousMutationAsync(
        Guid userId, Guid houseId, IReadOnlyList<MealPlanItem> candidates, CancellationToken cancellationToken) =>
        new("Conflict", Candidates: await ToCandidatesAsync(userId, houseId, candidates, cancellationToken),
            Message: "More than one meal occupies the requested slot; choose a meal id.");

    private async Task<MealPlanMutationResponse> AmbiguousDestinationAsync(
        Guid userId, Guid houseId, IReadOnlyList<MealPlanItem> candidates,
        DateOnly date, string slot, CancellationToken cancellationToken) =>
        candidates.Count switch
        {
            0 => new("Conflict", Message: "The destination slot is empty; swap requires exactly one destination meal."),
            1 => new("Conflict", Candidates: await ToCandidatesAsync(userId, houseId, candidates, cancellationToken),
                Message: "The destination slot is occupied; choose replace or swap."),
            _ => new("Conflict", Candidates: await ToCandidatesAsync(userId, houseId, candidates, cancellationToken),
                Message: $"The destination slot {date:yyyy-MM-dd} {slot} is ambiguous; choose a destination meal.")
        };

    private async Task<IReadOnlyList<MealPlanCandidate>> ToCandidatesAsync(
        Guid userId, Guid houseId, IReadOnlyList<MealPlanItem> items, CancellationToken cancellationToken)
    {
        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var result = new List<MealPlanCandidate>(items.Count);
        foreach (var item in items)
        {
            var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
            result.Add(new(item.Id, item.Date.ToString("yyyy-MM-dd"), response.Slot,
                response.Type, response.DisplayName, item.Version));
        }
        return result;
    }

    private static MealPlanMutationResponse ValidationMutation(IReadOnlyDictionary<string, string[]> errors) =>
        new("ValidationFailed", ValidationErrors: errors);

    private static MealPlanMutationResponse VersionConflict(long actualVersion) =>
        new("Conflict", ValidationErrors: new Dictionary<string, string[]>
        {
            ["version"] = [$"The meal has changed. Reload it and retry with version {actualVersion}."]
        });

    private static void SetMutationKey(MealPlanItem item, string? mutationKey)
    {
        if (!string.IsNullOrWhiteSpace(mutationKey))
            item.LastMutationKey = mutationKey.Trim();
    }

    private async Task<Dictionary<string, string[]>> ValidateAddAsync(
        Guid houseId, AddMealPlanItemRequest request, CancellationToken cancellationToken, bool validateSlot = true)
    {
        var errors = new Dictionary<string, string[]>();

        if (validateSlot && SlotError(request.Slot) is { } slotError)
            errors["slot"] = slotError["slot"];

        var hasRecipe = request.RecipeId.HasValue;
        var hasIngredient = request.IngredientId.HasValue;
        var hasPreparedMeal = request.PreparedMealId.HasValue;
        var hasFreeText = request.FreeText is not null;

        if ((hasRecipe ? 1 : 0) + (hasIngredient ? 1 : 0) + (hasPreparedMeal ? 1 : 0) + (hasFreeText ? 1 : 0) != 1)
            errors["item"] = ["Exactly one of recipeId, ingredientId, preparedMealId or freeText must be provided."];
        else if (hasFreeText && !request.ConfirmUncatalogued)
            errors["confirmUncatalogued"] = ["Free-text meals require explicit confirmation after recipe lookup."];
        else if (!hasFreeText && request.ConfirmUncatalogued)
            errors["confirmUncatalogued"] = ["confirmUncatalogued can only be used with freeText."];
        else if (!hasPreparedMeal && request.Addons is { Count: > 0 })
            errors["addons"] = ["Add-ons can only be selected for a prepared meal."];
        else if (hasRecipe)
        {
            var recipe = await recipeRepository.GetByIdAsync(request.RecipeId!.Value, houseId, tracked: false, cancellationToken);
            if (recipe is null) errors["recipeId"] = ["Recipe not found."];
        }
        else if (hasIngredient)
        {
            var ingredient = await ingredientRepository.GetByIdAsync(request.IngredientId!.Value, tracked: false, cancellationToken);
            if (ingredient is null) errors["ingredientId"] = ["Ingredient not found."];
        }
        else if (hasFreeText)
        {
            if (string.IsNullOrWhiteSpace(request.FreeText)) errors["freeText"] = ["Free text is required."];
            else if (request.FreeText.Trim().Length > 500) errors["freeText"] = ["Free text cannot exceed 500 characters."];
        }
        else if (preparedMealRepository is null || await preparedMealRepository.GetAsync(request.PreparedMealId!.Value, houseId, false, cancellationToken) is not { IsArchived: false } meal)
            errors["preparedMealId"] = ["Prepared meal not found or archived."];
        else
        {
            var selected = request.Addons ?? [];
            if (selected.GroupBy(a => a.IngredientId).Any(g => g.Count() > 1)) errors["addons"] = ["Duplicate add-ons are not allowed."];
            if (meal.Addons.Where(a => a.IsRequired).Any(required => selected.All(a => a.IngredientId != required.IngredientId))) errors["addons"] = ["Required add-ons must be selected."];
            foreach (var addon in selected)
            {
                if (addon.Quantity <= 0 || string.IsNullOrWhiteSpace(addon.Unit)) errors["addons"] = ["Add-on quantity and unit are required."];
                if (meal.Addons.All(a => a.IngredientId != addon.IngredientId)) errors["addons"] = ["Selected add-on is not configured for this prepared meal."];
            }
        }

        return errors;
    }

    private async Task<WeekValidation> ValidateWeekAsync(
        Guid houseId,
        AddWeeklyMealPlanRequest request,
        bool requireMeals,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (requireMeals && request.Meals.Count == 0)
            errors["meals"] = ["Add at least one meal to the weekly plan."];
        else if (request.Meals.Count > 35)
            errors["meals"] = ["A weekly plan cannot contain more than 35 meals."];

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
                errors[$"meals[{index}].date"] = ["Date must be within the seven-day week starting at weekStart."];
            if (SlotError(meal.Slot) is { } slotError)
                errors[$"meals[{index}].slot"] = slotError["slot"];

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
                errors[$"meals[{index}].addons"] = ["Add-ons can only be selected for a prepared meal."];
            else if (hasRecipe && !recipes.ContainsKey(meal.RecipeId!.Value))
                errors[$"meals[{index}].recipeId"] = ["Recipe not found."];
            else if (hasIngredient && !ingredients.ContainsKey(meal.IngredientId!.Value))
                errors[$"meals[{index}].ingredientId"] = ["Ingredient not found."];
            else if (hasFreeText && string.IsNullOrWhiteSpace(meal.FreeText))
                errors[$"meals[{index}].freeText"] = ["Free text is required."];
            else if (hasFreeText && meal.FreeText!.Trim().Length > 500)
                errors[$"meals[{index}].freeText"] = ["Free text cannot exceed 500 characters."];
            else if (hasPreparedMeal)
                ValidatePreparedMeal(index, meal, preparedMeals, errors);
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
            errors[$"meals[{index}].addons"] = ["Duplicate add-ons are not allowed."];
        if (meal.Addons.Where(addon => addon.IsRequired)
            .Any(required => selected.All(addon => addon.IngredientId != required.IngredientId)))
            errors[$"meals[{index}].addons"] = ["Required add-ons must be selected."];
        foreach (var addon in selected)
        {
            if (addon.Quantity <= 0 || string.IsNullOrWhiteSpace(addon.Unit))
                errors[$"meals[{index}].addons"] = ["Add-on quantity and unit are required."];
            if (meal.Addons.All(configured => configured.IngredientId != addon.IngredientId))
                errors[$"meals[{index}].addons"] = ["Selected add-on is not configured for this prepared meal."];
        }
    }

    private static MealPlanPreviewItem ToPreview(
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

    private static (string DisplayName, string Type) ResolveDisplay(
        MealPlanItem item, WeekLookupContext context)
    {
        if (item.RecipeId is { } recipeId)
            return (context.Recipes[recipeId].Title, "recipe");
        if (item.IngredientId is { } ingredientId)
            return (context.Ingredients[ingredientId].Name, "ingredient");
        if (item.FreeText is { } freeText)
            return (freeText, "free-text");
        return (context.PreparedMeals[item.PreparedMealId!.Value].Name, "prepared-meal");
    }

    private async Task<Dictionary<Guid, string>> GetMemberNamesAsync(Guid houseId, CancellationToken cancellationToken)
    {
        var members = await repository.GetHouseMembersAsync(houseId, cancellationToken);
        return members.ToDictionary(m => m.UserId, m => m.DisplayName);
    }

    private async Task<MealPlanItemResponse> ToResponseAsync(
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

    private static MealPlanItemResponse ToResponse(
        MealPlanItem item,
        Guid userId,
        IReadOnlyDictionary<Guid, string> memberNames,
        string displayName,
        string type) =>
        ToResponse(item, userId, memberNames, (displayName, type));

    private static MealPlanItemResponse ToResponse(
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
            SlotToString(item.Slot),
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

    private sealed record WeekLookupContext(
        IReadOnlyDictionary<Guid, Recipe> Recipes,
        IReadOnlyDictionary<Guid, Ingredient> Ingredients,
        IReadOnlyDictionary<Guid, PreparedMeal> PreparedMeals);

    private sealed record WeekValidation(
        Dictionary<string, string[]> Errors,
        WeekLookupContext Context);

    /// <summary>
    /// Validates a slot name, returning the slot validation error dictionary when it is
    /// missing or unknown, or null when it is valid. Shared by add and move so the set of
    /// valid slots and the message stay in one place.
    /// </summary>
    private static Dictionary<string, string[]>? SlotError(string? slot) =>
        string.IsNullOrWhiteSpace(slot) || !ValidSlots.Contains(slot.ToLower())
            ? new Dictionary<string, string[]> { ["slot"] = [$"Slot must be one of: {string.Join(", ", ValidSlots)}."] }
            : null;

    private static MealSlot ParseSlot(string slot) => slot.ToLower() switch
    {
        "breakfast" => MealSlot.Breakfast,
        "second-breakfast" => MealSlot.SecondBreakfast,
        "dinner" => MealSlot.Dinner,
        "snack" => MealSlot.Snack,
        "supper" => MealSlot.Supper,
        _ => throw new InvalidOperationException($"Invalid slot: {slot}")
    };

    private static string SlotToString(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "breakfast",
        MealSlot.SecondBreakfast => "second-breakfast",
        MealSlot.Dinner => "dinner",
        MealSlot.Snack => "snack",
        MealSlot.Supper => "supper",
        _ => throw new InvalidOperationException($"Unknown slot: {slot}")
    };

    private static (DateOnly, MealSlot, Guid?, Guid?, Guid?, string?) MealKey(MealPlanItem item) =>
        (item.Date, item.Slot, item.RecipeId, item.IngredientId, item.PreparedMealId, item.FreeText?.Trim());

    private void AddCopies(IReadOnlyList<MealPlanItem> source, Guid userId, Guid houseId,
        Func<MealPlanItem, DateOnly> targetDate)
    {
        var now = DateTimeOffset.UtcNow;
        var copies = source.ToDictionary(original => original.Id, original => new MealPlanItem
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            UserId = userId,
            Date = targetDate(original),
            Slot = original.Slot,
            RecipeId = original.RecipeId,
            IngredientId = original.IngredientId,
            PreparedMealId = original.PreparedMealId,
            FreeText = original.FreeText,
            IngredientQuantity = original.IngredientQuantity,
            IngredientUnit = original.IngredientUnit,
            Note = original.Note,
            SortOrder = original.SortOrder,
            Servings = original.Servings,
            Guests = original.Guests,
            CreatedAt = now,
            UpdatedAt = now,
            Participants = original.Participants.Select(participant => new MealPlanItemParticipant
            { UserId = participant.UserId, PortionPercent = participant.PortionPercent }).ToList()
        });
        foreach (var original in source)
        {
            var copy = copies[original.Id];
            copy.ParentMealPlanItemId = original.ParentMealPlanItemId is { } parentId ? copies[parentId].Id : null;
            repository.Add(copy);
        }
    }
}
