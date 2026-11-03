using Kotlet.Application.Ingredients;
using Kotlet.Application.PreparedMeals;
using Kotlet.Application.Recipes;
using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.MealPlanner;

/// <summary>Versioned, idempotent replace/move/swap/clear operations addressed by meal id or by date and slot.</summary>
public sealed class MealPlanSlotMutationService(
    IMealPlanRepository repository,
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IPreparedMealRepository? preparedMealRepository = null)
{
    private readonly MealPlanResponseBuilder _responses =
        new(repository, recipeRepository, ingredientRepository, preparedMealRepository);
    private readonly MealPlanItemValidator _validator =
        new(recipeRepository, ingredientRepository, preparedMealRepository);

    public async Task<MealPlanMutationResponse> ReplaceAsync(
        Guid userId, Guid houseId, ReplaceMealPlanRequest request, CancellationToken cancellationToken)
    {
        var sourceErrors = await _validator.ValidateSourceAsync(houseId, request.Source, cancellationToken);
        if (sourceErrors.Count > 0)
        {
            return ValidationMutation(sourceErrors);
        }

        MealPlanItem? item;
        if (request.MealId is { } mealId)
        {
            item = await repository.GetByIdAsync(mealId, houseId, cancellationToken);
            if (item is null)
            {
                return new("NotFound");
            }
        }
        else
        {
            if (!request.Date.HasValue)
            {
                return ValidationMutation(new Dictionary<string, string[]> { ["date"] = ["Date is required when mealId is omitted."] });
            }
            if (MealSlots.Error(request.Slot) is { } slotError)
            {
                return ValidationMutation(slotError);
            }

            var candidates = await GetSlotItemsAsync(houseId, request.Date.Value, MealSlots.Parse(request.Slot!), cancellationToken);
            if (candidates.Count > 1)
            {
                return await AmbiguousMutationAsync(userId, houseId, candidates, cancellationToken);
            }
            var candidate = candidates.SingleOrDefault();
            item = candidate is null
                ? null
                : await repository.GetByIdAsync(candidate.Id, houseId, cancellationToken);
            if (item is null)
            {
                if (!request.CreateIfMissing)
                {
                    return new("NotFound");
                }

                var now = DateTimeOffset.UtcNow;
                item = NewItem(userId, houseId, request.Date.Value, MealSlots.Parse(request.Slot!), candidates.Count, request.Source, now);
                SetMutationKey(item, request.IdempotencyKey);
                repository.Add(item);
                AddSelectedAddons(item, request.Source.Addons, now);
                await repository.SaveChangesAsync(cancellationToken);
                return new("Success", await _responses.ToResponseAsync(item, userId, houseId,
                    await _responses.GetMemberNamesAsync(houseId, cancellationToken), cancellationToken));
            }
        }

        if (item.ParentMealPlanItemId.HasValue)
        {
            return ValidationMutation(new Dictionary<string, string[]> { ["mealId"] = ["Meal-plan add-ons cannot be replaced directly."] });
        }
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
            string.Equals(item.LastMutationKey, request.IdempotencyKey.Trim(), StringComparison.Ordinal))
        {
            return await SuccessMutationAsync(userId, houseId, item, cancellationToken);
        }
        if (request.ExpectedVersion is { } expectedVersion && item.Version != expectedVersion)
        {
            return VersionConflict(item.Version);
        }
        if (SourceMatches(item, request.Source))
        {
            return new("Success", await _responses.ToResponseAsync(item, userId, houseId,
                await _responses.GetMemberNamesAsync(houseId, cancellationToken), cancellationToken));
        }

        foreach (var addon in item.AddonItems.ToList())
        {
            repository.Remove(addon);
        }
        item.AddonItems.Clear();
        ApplySource(item, request.Source);
        item.Version++;
        SetMutationKey(item, request.IdempotencyKey);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        AddSelectedAddons(item, request.Source.Addons, item.UpdatedAt);
        await repository.SaveChangesAsync(cancellationToken);

        return new("Success", await _responses.ToResponseAsync(item, userId, houseId,
            await _responses.GetMemberNamesAsync(houseId, cancellationToken), cancellationToken));
    }

    public async Task<MealPlanMutationResponse> MoveAsync(
        Guid userId, Guid houseId, MoveMealPlanMutationRequest request, CancellationToken cancellationToken)
    {
        if (MealSlots.Error(request.Slot) is { } slotError)
        {
            return ValidationMutation(slotError);
        }
        var targetSlot = MealSlots.Parse(request.Slot);
        var behavior = (request.DestinationBehavior ?? "reject").Trim().ToLowerInvariant();
        if (behavior is not ("reject" or "replace" or "swap"))
        {
            return ValidationMutation(new Dictionary<string, string[]> { ["destinationBehavior"] = ["Destination behavior must be reject, replace, or swap."] });
        }
        var mutationKey = request.IdempotencyKey?.Trim();

        var item = await repository.GetByIdAsync(request.MealId, houseId, cancellationToken);
        if (item is null)
        {
            return new("NotFound");
        }
        if (item.ParentMealPlanItemId.HasValue)
        {
            return ValidationMutation(new Dictionary<string, string[]> { ["mealId"] = ["Meal-plan add-ons cannot be moved directly."] });
        }
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
                {
                    return await SuccessPairMutationAsync(userId, houseId, item, pairedItem, cancellationToken);
                }
            }
            return await SuccessMutationAsync(userId, houseId, item, cancellationToken);
        }
        if (request.ExpectedVersion is { } expectedVersion && item.Version != expectedVersion)
        {
            return VersionConflict(item.Version);
        }

        if (item.Date == request.Date && item.Slot == targetSlot)
        {
            return await SuccessMutationAsync(userId, houseId, item, cancellationToken);
        }

        var candidates = (await GetSlotItemsAsync(houseId, request.Date, targetSlot, cancellationToken))
            .Where(candidate => candidate.Id != item.Id)
            .ToList();

        if (behavior == "swap")
        {
            if (candidates.Count != 1)
            {
                return await AmbiguousDestinationAsync(userId, houseId, candidates, request.Date, request.Slot, cancellationToken);
            }

            var destination = await repository.GetByIdAsync(candidates[0].Id, houseId, cancellationToken);
            if (destination is null)
            {
                return new("NotFound");
            }
            if (request.DestinationExpectedVersion is { } destinationVersion && destination.Version != destinationVersion)
            {
                return VersionConflict(destination.Version);
            }

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
            var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
            return new("Success", Items: [
                await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken),
                await _responses.ToResponseAsync(destination, userId, houseId, members, cancellationToken)
            ]);
        }

        if (candidates.Count > 0)
        {
            if (behavior == "reject" || candidates.Count > 1)
            {
                return await AmbiguousDestinationAsync(userId, houseId, candidates, request.Date, request.Slot, cancellationToken);
            }

            var destination = await repository.GetByIdAsync(candidates[0].Id, houseId, cancellationToken);
            if (destination is null)
            {
                return new("NotFound");
            }
            if (request.DestinationExpectedVersion is { } destinationVersion && destination.Version != destinationVersion)
            {
                return VersionConflict(destination.Version);
            }
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
        {
            return ValidationMutation(new Dictionary<string, string[]> { ["mealId"] = ["The two meals must be different."] });
        }

        var first = await repository.GetByIdAsync(request.FirstMealId, houseId, cancellationToken);
        var second = await repository.GetByIdAsync(request.SecondMealId, houseId, cancellationToken);
        if (first is null || second is null)
        {
            return new("NotFound");
        }
        if (first.ParentMealPlanItemId.HasValue || second.ParentMealPlanItemId.HasValue)
        {
            return ValidationMutation(new Dictionary<string, string[]> { ["mealId"] = ["Meal-plan add-ons cannot be swapped directly."] });
        }
        var mutationKey = request.IdempotencyKey?.Trim();
        if (!string.IsNullOrWhiteSpace(mutationKey) &&
            string.Equals(first.LastMutationKey, mutationKey, StringComparison.Ordinal) &&
            string.Equals(second.LastMutationKey, mutationKey, StringComparison.Ordinal))
        {
            return await SuccessPairMutationAsync(userId, houseId, first, second, cancellationToken);
        }
        if (request.FirstExpectedVersion is { } firstVersion && first.Version != firstVersion)
        {
            return VersionConflict(first.Version);
        }
        if (request.SecondExpectedVersion is { } secondVersion && second.Version != secondVersion)
        {
            return VersionConflict(second.Version);
        }
        if (first.Date == second.Date && first.Slot == second.Slot)
        {
            return await SuccessPairMutationAsync(userId, houseId, first, second, cancellationToken);
        }

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
        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        return new("Success", Items: [
            await _responses.ToResponseAsync(first, userId, houseId, members, cancellationToken),
            await _responses.ToResponseAsync(second, userId, houseId, members, cancellationToken)
        ]);
    }

    public async Task<MealPlanMutationResponse> ClearSlotAsync(
        Guid userId, Guid houseId, ClearMealPlanSlotRequest request, CancellationToken cancellationToken)
    {
        if (MealSlots.Error(request.Slot) is { } slotError)
        {
            return ValidationMutation(slotError);
        }

        var candidates = await GetSlotItemsAsync(houseId, request.Date, MealSlots.Parse(request.Slot), cancellationToken);
        if (candidates.Count == 0)
        {
            return new("Success", Items: [], Message: "The meal-plan slot is already empty.");
        }

        var tracked = new List<MealPlanItem>();
        foreach (var candidate in candidates)
        {
            if (await repository.GetByIdAsync(candidate.Id, houseId, cancellationToken) is { } item)
            {
                tracked.Add(item);
            }
        }

        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        var responses = new List<MealPlanItemResponse>();
        foreach (var item in tracked)
        {
            responses.Add(await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken));
            RemoveWithAddons(item);
        }
        await repository.SaveChangesAsync(cancellationToken);
        return new("Success", Items: responses);
    }

    public async Task<MealPlanRecommendationResponse> RecommendReplacementAsync(
        Guid houseId, DateOnly date, string slot, CancellationToken cancellationToken)
    {
        if (MealSlots.Error(slot) is not null)
        {
            throw new ArgumentException("Invalid meal-plan slot.", nameof(slot));
        }

        var candidates = await GetSlotItemsAsync(houseId, date, MealSlots.Parse(slot), cancellationToken);
        var recommendations = (await recipeRepository.GetAllForDuplicateCheckAsync(houseId, cancellationToken))
            .Take(5)
            .Select(recipe => new MealPlanRecommendation(
                "recipe", recipe.Id, null, null, null, recipe.Title,
                "Household recipe; options are not ranked by preferences.", $"kotlet://recipes/{recipe.Id}"))
            .ToList();
        if (preparedMealRepository is not null && recommendations.Count < 5)
        {
            recommendations.AddRange((await preparedMealRepository.ListAsync(houseId, includeArchived: false, cancellationToken))
                .Take(5 - recommendations.Count)
                .Select(meal => new MealPlanRecommendation(
                    "prepared-meal", null, null, meal.Id, null, meal.Name,
                    "Prepared household meal; options are not ranked by preferences.",
                    $"kotlet://prepared-meals/{meal.Id}")));
        }

        return new(date, slot.ToLowerInvariant(), recommendations, candidates.Count == 0);
    }

    public Task<MealPlanMutationResponse> ApplyReplacementAsync(
        Guid userId, Guid houseId, ApplyMealPlanReplacementRequest request, CancellationToken cancellationToken) =>
        ReplaceAsync(userId, houseId,
            new ReplaceMealPlanRequest(request.MealId, null, null, request.Source, request.ExpectedVersion, request.IdempotencyKey),
            cancellationToken);

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
        {
            repository.Remove(addon);
        }
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
        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        return new("Success", await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken));
    }

    private async Task<MealPlanMutationResponse> SuccessPairMutationAsync(
        Guid userId, Guid houseId, MealPlanItem first, MealPlanItem second, CancellationToken cancellationToken)
    {
        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        return new("Success", Items: [
            await _responses.ToResponseAsync(first, userId, houseId, members, cancellationToken),
            await _responses.ToResponseAsync(second, userId, houseId, members, cancellationToken)
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
        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        var result = new List<MealPlanCandidate>(items.Count);
        foreach (var item in items)
        {
            var response = await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken);
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
        {
            item.LastMutationKey = mutationKey.Trim();
        }
    }
}
