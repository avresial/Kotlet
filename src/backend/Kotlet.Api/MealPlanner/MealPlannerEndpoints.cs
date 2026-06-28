using Kotlet.Api.Auth;
using Kotlet.Application.MealPlanner;

namespace Kotlet.Api.MealPlanner;

public static class MealPlannerEndpoints
{
    public static IEndpointRouteBuilder MapMealPlannerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/meal-planner").WithTags("MealPlanner").RequireAuthorization();
        group.MapGet("", GetForDate).WithName("GetMealPlan");
        group.MapGet("/overview", GetOverview).WithName("GetMealPlanOverview");
        group.MapGet("/members", GetMembers).WithName("GetMealPlanHouseMembers");
        group.MapPost("/items", AddItem).WithName("AddMealPlanItem");
        group.MapDelete("/items/{id:guid}", RemoveItem).WithName("RemoveMealPlanItem");
        group.MapPut("/items/{id:guid}/participants", SetParticipants).WithName("SetMealPlanItemParticipants");
        group.MapPut("/items/{id:guid}/servings", SetServings).WithName("SetMealPlanItemServings");
        return endpoints;
    }

    private static async Task<IResult> GetForDate(
        string? date,
        ICurrentUser currentUser,
        MealPlannerService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId) return Results.Unauthorized();

        if (!DateOnly.TryParse(date, out var parsedDate))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["date"] = ["date query parameter is required and must be in yyyy-MM-dd format."] });

        return Results.Ok(await service.GetForDateAsync(userId, houseId, parsedDate, cancellationToken));
    }

    private static async Task<IResult> GetMembers(
        ICurrentUser currentUser,
        MealPlannerService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        return Results.Ok(await service.GetHouseMembersAsync(houseId, cancellationToken));
    }

    private static async Task<IResult> GetOverview(
        string? from,
        int days,
        ICurrentUser currentUser,
        MealPlannerService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        if (!DateOnly.TryParse(from, out var parsedFrom))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["from"] = ["from query parameter is required and must be in yyyy-MM-dd format."] });
        if (days is < 1 or > 62)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["days"] = ["days must be between 1 and 62."] });

        return Results.Ok(await service.GetOverviewAsync(houseId, parsedFrom, days, cancellationToken));
    }

    private static async Task<IResult> AddItem(
        AddMealPlanItemRequest request,
        ICurrentUser currentUser,
        MealPlannerService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var result = await service.AddItemAsync(userId, houseId, request, cancellationToken);
        return result.Status switch
        {
            MealPlannerOperationStatus.Success => Results.Created($"/api/meal-planner/items/{result.Item!.Id}", result.Item),
            MealPlannerOperationStatus.ValidationFailed => Results.ValidationProblem(result.ValidationErrors!),
            _ => throw new InvalidOperationException($"Unsupported status: {result.Status}")
        };
    }

    private static async Task<IResult> RemoveItem(
        Guid id,
        ICurrentUser currentUser,
        MealPlannerService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        return await service.RemoveItemAsync(houseId, id, cancellationToken) is MealPlannerOperationStatus.Success
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static async Task<IResult> SetParticipants(
        Guid id,
        SetParticipantsRequest request,
        ICurrentUser currentUser,
        MealPlannerService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var result = await service.SetParticipantsAsync(userId, houseId, id, request.UserIds ?? [], cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> SetServings(
        Guid id,
        SetServingsRequest request,
        ICurrentUser currentUser,
        MealPlannerService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var result = await service.SetServingsAsync(userId, houseId, id, request.Servings, cancellationToken);
        return ToResult(result);
    }

    private static IResult ToResult(MealPlannerOperationResult result) => result.Status switch
    {
        MealPlannerOperationStatus.Success => Results.Ok(result.Item),
        MealPlannerOperationStatus.ValidationFailed => Results.ValidationProblem(result.ValidationErrors!),
        MealPlannerOperationStatus.NotFound => Results.NotFound(),
        _ => throw new InvalidOperationException($"Unsupported status: {result.Status}")
    };
}
