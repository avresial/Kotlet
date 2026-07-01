using Kotlet.Api.Auth;
using Kotlet.Application.FoodSettings;

namespace Kotlet.Api.FoodSettings;

public static class FoodSettingsEndpoints
{
    public static IEndpointRouteBuilder MapFoodSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users/me/food-settings").WithTags("Food settings").RequireAuthorization();
        group.MapGet("", async (ICurrentUser user, UserFoodSettingsService service, CancellationToken ct) =>
            user.UserId is { } id ? Results.Ok(await service.GetAsync(id, ct)) : Results.Unauthorized());
        group.MapPut("", async (SaveUserFoodSettingsCommand command, ICurrentUser user, UserFoodSettingsService service, CancellationToken ct) =>
        {
            if (user.UserId is not { } id) return Results.Unauthorized();
            var result = await service.SaveAsync(id, command, ct);
            return result.ValidationErrors is null ? Results.Ok(result.Settings) : Results.ValidationProblem(result.ValidationErrors);
        });
        return endpoints;
    }
}
