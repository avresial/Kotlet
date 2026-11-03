using Kotlet.Application.Ingredients;
using Kotlet.Application.PreparedMeals;
using Kotlet.Application.Recipes;
using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.MealPlanner;

/// <summary>Manages who eats a planned meal: participants, portions, guests and serving overrides.</summary>
public sealed class MealParticipantService(
    IMealPlanRepository repository,
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IPreparedMealRepository? preparedMealRepository = null)
{
    private const int MaxServings = 99;
    private const int MaxGuests = 99;

    private readonly MealPlanResponseBuilder _responses =
        new(repository, recipeRepository, ingredientRepository, preparedMealRepository);

    /// <summary>
    /// Replaces the set of people assigned to a meal. Passing the full list of house
    /// member ids implements the "add whole house" action; passing a subset removes the
    /// omitted members. All ids must belong to the current user's house.
    /// </summary>
    public async Task<MealPlannerOperationResult> SetParticipantsAsync(
        Guid userId, Guid houseId, Guid itemId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null)
        {
            return new(MealPlannerOperationStatus.NotFound);
        }

        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        var desired = userIds.Distinct().ToHashSet();

        var unknown = desired.Where(id => !members.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]>
            {
                ["userIds"] = ["One or more selected people are not members of your house."]
            });
        }

        foreach (var participant in item.Participants.Where(p => !desired.Contains(p.UserId)).ToList())
        {
            item.Participants.Remove(participant);
        }

        var current = item.Participants.Select(p => p.UserId).ToHashSet();
        foreach (var id in desired.Where(id => !current.Contains(id)))
        {
            item.Participants.Add(new MealPlanItemParticipant { MealPlanItemId = item.Id, UserId = id });
        }

        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await repository.SaveChangesAsync(cancellationToken);

        var response = await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken);
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
        if (item is null)
        {
            return new(MealPlannerOperationStatus.NotFound);
        }

        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        var requested = userIds.Distinct().ToList();

        var unknown = requested.Where(id => !members.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]>
            {
                ["userIds"] = ["One or more selected people are not members of your house."]
            });
        }

        var current = item.Participants.Select(p => p.UserId).ToHashSet();
        foreach (var id in requested.Where(id => !current.Contains(id)))
        {
            item.Participants.Add(new MealPlanItemParticipant { MealPlanItemId = item.Id, UserId = id });
        }

        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await repository.SaveChangesAsync(cancellationToken);

        var response = await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }

    public async Task<MealPlannerOperationResult> SetParticipantPortionAsync(
        Guid userId, Guid houseId, Guid itemId, Guid participantUserId, int portionPercent,
        CancellationToken cancellationToken)
    {
        if (portionPercent is < MealPlanItemParticipant.MinPortionPercent or > MealPlanItemParticipant.MaxPortionPercent)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]>
            {
                ["portionPercent"] = ["Portion percent must be between 50 and 150."]
            });
        }

        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null)
        {
            return new(MealPlannerOperationStatus.NotFound);
        }

        var participant = item.Participants.SingleOrDefault(value => value.UserId == participantUserId);
        if (participant is null)
        {
            return new(MealPlannerOperationStatus.NotFound);
        }

        participant.PortionPercent = portionPercent;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await repository.SaveChangesAsync(cancellationToken);

        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        return new(MealPlannerOperationStatus.Success,
            await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken));
    }

    /// <summary>
    /// Sets an explicit serving count for a meal, or clears it (null) so the count is
    /// derived from the number of assigned participants.
    /// </summary>
    public async Task<MealPlannerOperationResult> SetServingsAsync(
        Guid userId, Guid houseId, Guid itemId, int? servings, CancellationToken cancellationToken)
    {
        if (servings is < 0 or > MaxServings)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]>
            {
                ["servings"] = [$"Servings must be between 0 and {MaxServings}."]
            });
        }

        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null)
        {
            return new(MealPlannerOperationStatus.NotFound);
        }

        item.Servings = servings;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await repository.SaveChangesAsync(cancellationToken);

        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        var response = await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken);
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
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]>
            {
                ["guests"] = [$"Guests must be between 0 and {MaxGuests}."]
            });
        }

        var item = await repository.GetByIdAsync(itemId, houseId, cancellationToken);
        if (item is null)
        {
            return new(MealPlannerOperationStatus.NotFound);
        }

        item.Guests = guests;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await repository.SaveChangesAsync(cancellationToken);

        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        var response = await _responses.ToResponseAsync(item, userId, houseId, members, cancellationToken);
        return new(MealPlannerOperationStatus.Success, response);
    }
}
