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
    private const int MaxServings = 99;

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
                ["dinner"] = responses.Where(r => r.Slot == "dinner").OrderBy(r => r.SortOrder).ToList(),
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

    private async Task<Dictionary<string, string[]>> ValidateAddAsync(
        Guid houseId, AddMealPlanItemRequest request, CancellationToken cancellationToken)
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
            item.EffectiveServings,
            item.Servings.HasValue);
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
