using Kotlet.Application.Ingredients;
using Kotlet.Application.Recipes;
using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.MealPlanner;

public sealed class MealPlannerService(
    IMealPlanRepository repository,
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository)
{
    private static readonly HashSet<string> ValidSlots = ["breakfast", "second-breakfast", "dinner", "snack", "supper"];
    private const int MaxServings = 99;
    private const int MaxGuests = 99;

    public async Task<DailyMealPlanResponse> GetForDateAsync(
        Guid userId, Guid houseId, DateOnly date, CancellationToken cancellationToken)
    {
        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var items = await repository.GetByDateAsync(houseId, date, cancellationToken);
        var responses = new List<MealPlanItemResponse>();
        foreach (var item in items)
        {
            var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
            responses.Add(response);
        }

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
    }

    public async Task<IReadOnlyList<MealHouseMember>> GetHouseMembersAsync(
        Guid houseId, CancellationToken cancellationToken) =>
        await repository.GetHouseMembersAsync(houseId, cancellationToken);

    public async Task<IReadOnlyList<MealPlanOverviewDay>> GetOverviewAsync(
        Guid houseId, DateOnly from, int days, CancellationToken cancellationToken)
    {
        var to = from.AddDays(days - 1);
        var items = await repository.GetByDateRangeAsync(houseId, from, to, cancellationToken);
        var slotsByDate = items
            .GroupBy(item => item.Date)
            .ToDictionary(group => group.Key, group => group.Select(item => SlotToString(item.Slot)).Distinct().ToHashSet());

        return Enumerable.Range(0, days)
            .Select(offset => from.AddDays(offset))
            .Select(date => new MealPlanOverviewDay(
                date.ToString("yyyy-MM-dd"),
                slotsByDate.TryGetValue(date, out var slots) ? slots.Order().ToList() : []))
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
            Note = request.Note?.Trim(),
            SortOrder = existingCount,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        repository.Add(item);
        await repository.SaveChangesAsync(cancellationToken);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    public async Task<WeeklyMealPlannerOperationResult> AddWeekAsync(
        Guid userId, Guid houseId, AddWeeklyMealPlanRequest request, CancellationToken cancellationToken)
    {
        if (request.Meals.Count > 35)
            return WeeklyValidation("meals", "A weekly plan cannot contain more than 35 meals.");

        var errors = new Dictionary<string, string[]>();
        for (var index = 0; index < request.Meals.Count; index++)
        {
            var meal = request.Meals[index];
            if (meal.Date < request.WeekStart || meal.Date > request.WeekStart.AddDays(6))
                errors[$"meals[{index}].date"] = ["Date must be within the seven-day week starting at weekStart."];

            foreach (var (field, messages) in await ValidateAddAsync(houseId, meal, cancellationToken))
                errors[$"meals[{index}].{field}"] = messages;
        }
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
            var key = (meal.Date, slot, meal.RecipeId, meal.IngredientId);
            if (!keys.Add(key))
            {
                skipped++;
                continue;
            }

            var slotKey = (meal.Date, slot);
            var item = new MealPlanItem
            {
                Id = Guid.NewGuid(), HouseId = houseId, UserId = userId, Date = meal.Date,
                Slot = slot, RecipeId = meal.RecipeId, IngredientId = meal.IngredientId,
                Note = meal.Note?.Trim(), SortOrder = slotCounts.GetValueOrDefault(slotKey),
                CreatedAt = now, UpdatedAt = now
            };
            slotCounts[slotKey] = item.SortOrder + 1;
            repository.Add(item);
            added.Add(item);
        }

        if (added.Count > 0)
            await repository.SaveChangesAsync(cancellationToken);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var responses = new List<MealPlanItemResponse>(added.Count);
        foreach (var item in added)
            responses.Add(await ToResponseAsync(item, userId, houseId, members, cancellationToken));
        return new(MealPlannerOperationStatus.Success, new(responses, skipped));
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

        var now = DateTimeOffset.UtcNow;
        foreach (var original in source)
        {
            var copy = new MealPlanItem
            {
                Id = Guid.NewGuid(), HouseId = houseId, UserId = userId, Date = request.TargetDate,
                Slot = original.Slot, RecipeId = original.RecipeId, IngredientId = original.IngredientId,
                Note = original.Note, SortOrder = original.SortOrder, Servings = original.Servings,
                Guests = original.Guests, CreatedAt = now, UpdatedAt = now,
                Participants = original.Participants.Select(participant => new MealPlanItemParticipant
                    { UserId = participant.UserId }).ToList()
            };
            repository.Add(copy);
        }

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
        var now = DateTimeOffset.UtcNow;
        foreach (var original in source)
        {
            repository.Add(new MealPlanItem
            {
                Id = Guid.NewGuid(), HouseId = houseId, UserId = userId, Date = original.Date.AddDays(offset),
                Slot = original.Slot, RecipeId = original.RecipeId, IngredientId = original.IngredientId,
                Note = original.Note, SortOrder = original.SortOrder, Servings = original.Servings,
                Guests = original.Guests, CreatedAt = now, UpdatedAt = now,
                Participants = original.Participants.Select(participant => new MealPlanItemParticipant
                    { UserId = participant.UserId }).ToList()
            });
        }

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
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await repository.SaveChangesAsync(cancellationToken);
        }

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

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
        await repository.SaveChangesAsync(cancellationToken);

        var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
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
        await repository.SaveChangesAsync(cancellationToken);

        var members = await GetMemberNamesAsync(houseId, cancellationToken);
        var response = await ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    private async Task<Dictionary<string, string[]>> ValidateAddAsync(
        Guid houseId, AddMealPlanItemRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (SlotError(request.Slot) is { } slotError)
            errors["slot"] = slotError["slot"];

        var hasRecipe = request.RecipeId.HasValue;
        var hasIngredient = request.IngredientId.HasValue;

        if (!hasRecipe && !hasIngredient)
            errors["item"] = ["Either recipeId or ingredientId must be provided."];
        else if (hasRecipe && hasIngredient)
            errors["item"] = ["Only one of recipeId or ingredientId may be provided."];
        else if (hasRecipe)
        {
            var recipe = await recipeRepository.GetByIdAsync(request.RecipeId!.Value, houseId, tracked: false, cancellationToken);
            if (recipe is null) errors["recipeId"] = ["Recipe not found."];
        }
        else
        {
            var ingredient = await ingredientRepository.GetByIdAsync(request.IngredientId!.Value, tracked: false, cancellationToken);
            if (ingredient is null) errors["ingredientId"] = ["Ingredient not found."];
        }

        return errors;
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
        else
        {
            var ingredient = await ingredientRepository.GetByIdAsync(item.IngredientId!.Value, tracked: false, cancellationToken);
            displayName = ingredient?.Name ?? "Unknown ingredient";
            type = "ingredient";
        }

        var participants = item.Participants
            .Select(p => new MealParticipantResponse(
                p.UserId,
                memberNames.TryGetValue(p.UserId, out var name) ? name : "Unknown",
                p.UserId == userId))
            .OrderByDescending(p => p.IsCurrentUser)
            .ThenBy(p => p.DisplayName)
            .ToList();

        return new MealPlanItemResponse(
            item.Id,
            SlotToString(item.Slot),
            type,
            item.RecipeId,
            item.IngredientId,
            displayName,
            item.Note,
            item.SortOrder,
            participants,
            item.Guests,
            item.EffectiveServings,
            item.Servings.HasValue);
    }

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

    private static (DateOnly, MealSlot, Guid?, Guid?) MealKey(MealPlanItem item) =>
        (item.Date, item.Slot, item.RecipeId, item.IngredientId);

    private static WeeklyMealPlannerOperationResult WeeklyValidation(string field, string message) =>
        new(MealPlannerOperationStatus.ValidationFailed,
            ValidationErrors: new Dictionary<string, string[]> { [field] = [message] });
}
