using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.MealPlanner;

/// <summary>Copies a planned day or week onto an empty target day or week.</summary>
public sealed class MealPlanCopyService(
    IMealPlanRepository repository,
    MealPlanQueryService queries)
{
    public async Task<CopyMealPlanDayResult> CopyDayAsync(
        Guid userId, Guid houseId, CopyMealPlanDayRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceDate == request.TargetDate)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors:
                new Dictionary<string, string[]> { ["targetDate"] = ["Target date must differ from source date."] });
        }

        var source = await repository.GetByDateAsync(houseId, request.SourceDate, cancellationToken);
        if (source.Count == 0)
        {
            return new(MealPlannerOperationStatus.NotFound);
        }
        if ((await repository.GetByDateAsync(houseId, request.TargetDate, cancellationToken)).Count > 0)
        {
            return new(MealPlannerOperationStatus.Conflict);
        }

        AddCopies(source, userId, houseId, _ => request.TargetDate);

        await repository.SaveChangesAsync(cancellationToken);
        return new(MealPlannerOperationStatus.Success,
            await queries.GetForDateAsync(userId, houseId, request.TargetDate, cancellationToken));
    }

    public async Task<CopyMealPlanWeekResult> CopyWeekAsync(
        Guid userId, Guid houseId, CopyMealPlanWeekRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceWeekStart.DayOfWeek != DayOfWeek.Monday || request.TargetWeekStart.DayOfWeek != DayOfWeek.Monday)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors:
                new Dictionary<string, string[]> { ["weekStart"] = ["Source and target weeks must start on Monday."] });
        }
        if (request.SourceWeekStart == request.TargetWeekStart)
        {
            return new(MealPlannerOperationStatus.ValidationFailed, ValidationErrors:
                new Dictionary<string, string[]> { ["targetWeekStart"] = ["Target week must differ from source week."] });
        }

        var source = await repository.GetByDateRangeAsync(houseId, request.SourceWeekStart, request.SourceWeekStart.AddDays(6), cancellationToken);
        if (source.Count == 0)
        {
            return new(MealPlannerOperationStatus.NotFound);
        }
        if ((await repository.GetByDateRangeAsync(houseId, request.TargetWeekStart, request.TargetWeekStart.AddDays(6), cancellationToken)).Count > 0)
        {
            return new(MealPlannerOperationStatus.Conflict);
        }

        var offset = request.TargetWeekStart.DayNumber - request.SourceWeekStart.DayNumber;
        AddCopies(source, userId, houseId, original => original.Date.AddDays(offset));

        await repository.SaveChangesAsync(cancellationToken);
        return new(MealPlannerOperationStatus.Success, source.Count);
    }

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
