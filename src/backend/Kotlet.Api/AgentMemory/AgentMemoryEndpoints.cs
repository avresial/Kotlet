using Kotlet.Api.Auth;
using Kotlet.Application.AgentMemory;
using Microsoft.AspNetCore.Mvc;

namespace Kotlet.Api.AgentMemory;

public static class AgentMemoryEndpoints
{
    public static IEndpointRouteBuilder MapAgentMemoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/agent-memory").WithTags("Agent memory").RequireAuthorization();
        group.MapGet("", async (
            ICurrentUser user, AgentMemoryService service, CancellationToken ct,
            string? q, string? category, string? source, string? reviewStatus, bool includeExpired = false) =>
        {
            if (user.UserId is not { } userId || user.HouseId is not { } houseId) return Results.Unauthorized();
            var result = await service.ListAsync(userId, houseId, q, category, source, reviewStatus, includeExpired, ct);
            return result.ValidationErrors is null ? Results.Ok(result) : Results.ValidationProblem(result.ValidationErrors);
        });
        group.MapGet("/changes", async (long since, ICurrentUser user, AgentMemoryService service, CancellationToken ct) =>
            user.UserId is not { } userId || user.HouseId is not { } houseId
                ? Results.Unauthorized()
                : since < 0
                    ? Results.BadRequest(new { message = "since must be zero or greater." })
                    : Results.Ok(await service.ChangesAsync(userId, houseId, since, ct)));
        group.MapGet("/export", async (ICurrentUser user, AgentMemoryService service, CancellationToken ct) =>
            user.UserId is not { } userId || user.HouseId is not { } houseId
                ? Results.Unauthorized()
                : Results.Ok(await service.ExportAsync(userId, houseId, ct)));
        group.MapGet("/{id:guid}", async (Guid id, ICurrentUser user, AgentMemoryService service, CancellationToken ct) =>
            user.UserId is not { } userId || user.HouseId is not { } houseId
                ? Results.Unauthorized()
                : await service.GetAsync(id, userId, houseId, ct) is { } memory
                    ? Results.Ok(memory)
                    : Results.NotFound());
        group.MapPost("", async ([FromBody] CreateAgentMemoryRequest request, ICurrentUser user, AgentMemoryService service, CancellationToken ct) =>
            user.UserId is not { } userId || user.HouseId is not { } houseId
                ? Results.Unauthorized()
                : ToHttpResult(await service.CreateAsync(userId, houseId, request, ct), true));
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateAgentMemoryRequest request, ICurrentUser user, AgentMemoryService service, CancellationToken ct) =>
            user.UserId is not { } userId || user.HouseId is not { } houseId
                ? Results.Unauthorized()
                : ToHttpResult(await service.UpdateAsync(id, userId, houseId, request, ct), false));
        group.MapDelete("/{id:guid}", async (Guid id, long expectedVersion, string? clientId, ICurrentUser user, AgentMemoryService service, CancellationToken ct) =>
            user.UserId is not { } userId || user.HouseId is not { } houseId
                ? Results.Unauthorized()
                : ToHttpResult(await service.DeleteAsync(id, userId, houseId, expectedVersion, clientId, ct), false));
        return endpoints;
    }

    private static IResult ToHttpResult(AgentMemoryOperationResult result, bool created) => result.Status switch
    {
        nameof(AgentMemoryOperationStatus.Success) when created => Results.Created($"/api/agent-memory/{result.Memory!.Id}", result),
        nameof(AgentMemoryOperationStatus.Success) => Results.Ok(result),
        nameof(AgentMemoryOperationStatus.NotFound) => Results.NotFound(),
        nameof(AgentMemoryOperationStatus.Conflict) => Results.Conflict(result),
        nameof(AgentMemoryOperationStatus.ValidationFailed) => Results.ValidationProblem(result.ValidationErrors!),
        _ => Results.BadRequest(result)
    };
}
