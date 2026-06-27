using Kotlet.Api.Auth;
using Kotlet.Application.MealPlanner;

namespace Kotlet.Api.MealPlanner;

public static class MealPlannerEndpoints
{
    public static IEndpointRouteBuilder MapMealPlannerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/meal-planner").WithTags("MealPlanner").RequireAuthorization();
        group.MapGet("", GetForDate).WithName("GetMealPlan");
        group.MapPost("/items", AddItem).WithName("AddMealPlanItem");
        group.MapDelete("/items/{id:guid}", RemoveItem).WithName("RemoveMealPlanItem");
        return endpoints;
    }

    private static async Task<IResult> GetForDate(
        string? date,
        ICurrentUser currentUser,
        MealPlannerService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();

        if (!DateOnly.TryParse(date, out var parsedDate))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["date"] = ["date query parameter is required and must be in yyyy-MM-dd format."] });

        return Results.Ok(await service.GetForDateAsync(userId, parsedDate, cancellationToken));
    }

    private static async Task<IResult> AddItem(
        AddMealPlanItemRequest request,
        ICurrentUser currentUser,
        MealPlannerService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.AddItemAsync(userId, request, cancellationToken);
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
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        return await service.RemoveItemAsync(userId, id, cancellationToken) is MealPlannerOperationStatus.Success
            ? Results.NoContent()
            : Results.NotFound();
    }
}
