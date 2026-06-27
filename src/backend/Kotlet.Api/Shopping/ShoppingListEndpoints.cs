using Kotlet.Api.Auth;
using Kotlet.Application.Shopping;

namespace Kotlet.Api.Shopping;

public static class ShoppingListEndpoints
{
    public static IEndpointRouteBuilder MapShoppingListEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/shopping-list").WithTags("Shopping list").RequireAuthorization();
        group.MapGet("", async (ICurrentUser user, ShoppingListService service, CancellationToken ct) =>
            user.HouseId is { } houseId ? Results.Ok(await service.GetAllAsync(houseId, ct)) : Results.Unauthorized());
        group.MapPost("", async (CreateShoppingListItemCommand command, ICurrentUser user, ShoppingListService service, CancellationToken ct) =>
            user.HouseId is { } houseId ? ToHttpResult(await service.CreateAsync(houseId, command, ct), true) : Results.Unauthorized());
        group.MapDelete("/checked", async (ICurrentUser user, ShoppingListService service, CancellationToken ct) =>
            user.HouseId is { } houseId ? Results.Ok(new { removed = await service.ClearPurchasedAsync(houseId, ct) }) : Results.Unauthorized());
        group.MapPut("/{id:guid}", async (Guid id, UpdateShoppingListItemCommand command, ICurrentUser user, ShoppingListService service, CancellationToken ct) =>
            user.HouseId is { } houseId ? ToHttpResult(await service.UpdateAsync(id, houseId, command, ct), false) : Results.Unauthorized());
        group.MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, ShoppingListService service, CancellationToken ct) =>
            user.HouseId is not { } houseId ? Results.Unauthorized() :
                await service.DeleteAsync(id, houseId, ct) == ShoppingListOperationStatus.Success ? Results.NoContent() : Results.NotFound());
        return endpoints;
    }

    private static IResult ToHttpResult(ShoppingListOperationResult result, bool created) => result.Status switch
    {
        ShoppingListOperationStatus.Success when created => Results.Created($"/api/shopping-list/{result.Item!.Id}", result.Item),
        ShoppingListOperationStatus.Success => Results.Ok(result.Item),
        ShoppingListOperationStatus.NotFound => Results.NotFound(),
        ShoppingListOperationStatus.Conflict => Results.Conflict(new { result.Message }),
        ShoppingListOperationStatus.ValidationFailed => Results.ValidationProblem(result.ValidationErrors!),
        _ => throw new InvalidOperationException()
    };
}
