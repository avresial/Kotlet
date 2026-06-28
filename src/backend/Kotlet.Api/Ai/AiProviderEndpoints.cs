using Kotlet.Api.Auth;
using Kotlet.Application.Ai;

namespace Kotlet.Api.Ai;

public static class AiProviderEndpoints
{
    public static IEndpointRouteBuilder MapAiProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ai-provider").WithTags("AI provider").RequireAuthorization();
        group.MapGet("", async (ICurrentUser user, UserAiProviderService service, CancellationToken ct) =>
            user.UserId is not { } userId ? Results.Unauthorized() :
            await service.GetAsync(userId, ct) is { } configuration ? Results.Ok(configuration) : Results.NotFound());
        group.MapPut("", Save);
        group.MapDelete("", async (ICurrentUser user, UserAiProviderService service, CancellationToken ct) =>
            user.UserId is not { } userId ? Results.Unauthorized() :
            await service.DeleteAsync(userId, ct) ? Results.NoContent() : Results.NotFound());
        return endpoints;
    }

    private static async Task<IResult> Save(
        SaveAiProviderConfigurationCommand command, ICurrentUser user, UserAiProviderService service, CancellationToken ct)
    {
        if (user.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.SaveAsync(userId, command, ct);
        return result.ValidationErrors is null ? Results.Ok(result.Configuration) : Results.ValidationProblem(result.ValidationErrors);
    }
}
