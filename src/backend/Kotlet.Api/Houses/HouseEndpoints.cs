using Kotlet.Api.Auth;
using Kotlet.Application.Houses;
using Kotlet.Api.Localization;
using Kotlet.Application.Pantry;
using Kotlet.Application.Recipes;

namespace Kotlet.Api.Houses;

public static class HouseEndpoints
{
    public static IEndpointRouteBuilder MapHouseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var houses = endpoints.MapGroup("/api/houses").WithTags("Houses").RequireAuthorization();
        endpoints.MapGet("/api/dashboard/stats", DashboardStats).WithTags("Dashboard").RequireAuthorization();
        houses.MapGet("", ListHouses);
        houses.MapPost("", CreateHouse);
        houses.MapGet("/invitations", ListMyInvitations);
        houses.MapPost("/invitations/{invitationId:guid}/accept", AcceptInvitation);
        houses.MapPost("/invitations/{invitationId:guid}/decline", DeclineInvitation);
        houses.MapGet("/{id:guid}", GetHouse);
        houses.MapPut("/{id:guid}", RenameHouse);
        houses.MapDelete("/{id:guid}", DeleteHouse);
        houses.MapPost("/{id:guid}/switch", SwitchHouse);
        houses.MapPost("/{id:guid}/members", InviteMember);
        houses.MapDelete("/{id:guid}/members/{memberUserId:guid}", RemoveMember);
        houses.MapDelete("/{id:guid}/invitations/{invitationId:guid}", CancelInvitation);
        return endpoints;
    }

    private static async Task<IResult> DashboardStats(ICurrentUser currentUser, RecipeService recipes,
        PantryService pantry, ILanguageContext language, CancellationToken ct)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var recipeTask = recipes.ListAsync(houseId, 1, 1, null, null, ct);
        var pantryTask = pantry.GetAllAsync(houseId, language.Language, ct);
        await Task.WhenAll(recipeTask, pantryTask);
        return Results.Ok(new DashboardStatsResponse(recipeTask.Result.TotalCount, pantryTask.Result.Count));
    }

    private static async Task<IResult> ListHouses(ICurrentUser currentUser, HouseService service, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        return Results.Ok(await service.ListAsync(userId, currentUser.HouseId, ct));
    }

    private static async Task<IResult> CreateHouse(CreateHouseRequest request, ICurrentUser currentUser,
        HouseService service, HouseSessionService sessions, HttpContext context, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.CreateAsync(userId, request, ct);
        if (result.Status == HouseOperationStatus.ValidationFailed) return Results.ValidationProblem(result.ValidationErrors!);
        if (result.Status == HouseOperationStatus.UserNotFound) return Results.Unauthorized();
        var token = result.ActivateHouse ? await sessions.ActivateAsync(userId, result.ActiveHouseId, context, ct) : null;
        return Results.Created($"/api/houses/{result.House!.Id}", new HouseWithTokenResponse(result.House, token));
    }

    private static async Task<IResult> GetHouse(Guid id, ICurrentUser currentUser, HouseService service, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var house = await service.GetAsync(userId, id, ct);
        return house is null ? Results.NotFound() : Results.Ok(house);
    }

    private static async Task<IResult> RenameHouse(Guid id, RenameHouseRequest request, ICurrentUser currentUser,
        HouseService service, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.RenameAsync(userId, id, request, ct);
        return result.Status switch
        {
            HouseOperationStatus.Success => Results.NoContent(),
            HouseOperationStatus.ValidationFailed => Results.ValidationProblem(result.ValidationErrors!),
            _ => Results.NotFound()
        };
    }

    private static async Task<IResult> DeleteHouse(Guid id, ICurrentUser currentUser, HouseService service,
        HouseSessionService sessions, HttpContext context, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.DeleteAsync(userId, id, ct);
        if (result.Status == HouseOperationStatus.NotFound) return Results.NotFound();
        if (!result.ActivateHouse) return Results.NoContent();
        var token = await sessions.ActivateAsync(userId, result.ActiveHouseId, context, ct);
        return token is null ? Results.NoContent() : Results.Ok(token);
    }

    private static async Task<IResult> SwitchHouse(Guid id, ICurrentUser currentUser, HouseService service,
        HouseSessionService sessions, HttpContext context, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.SwitchAsync(userId, id, ct);
        if (result.Status == HouseOperationStatus.NotFound) return Results.NotFound();
        var token = await sessions.ActivateAsync(userId, id, context, ct);
        return token is null ? Results.Unauthorized() : Results.Ok(token);
    }

    private static async Task<IResult> InviteMember(Guid id, InviteMemberRequest request, ICurrentUser currentUser,
        HouseService service, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.InviteAsync(userId, id, request, ct);
        return result.Status switch
        {
            HouseOperationStatus.Success => Results.Ok(result.Invitation),
            HouseOperationStatus.ValidationFailed => Results.ValidationProblem(result.ValidationErrors!),
            HouseOperationStatus.Conflict => Results.Conflict(new { result.Message }),
            _ => Results.NotFound(result.Message is null ? null : new { result.Message })
        };
    }

    private static async Task<IResult> RemoveMember(Guid id, Guid memberUserId, ICurrentUser currentUser,
        HouseService service, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        return await service.RemoveMemberAsync(userId, id, memberUserId, ct) is HouseOperationStatus.Success
            ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> CancelInvitation(Guid id, Guid invitationId, ICurrentUser currentUser,
        HouseService service, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        return await service.CancelInvitationAsync(userId, id, invitationId, ct) is HouseOperationStatus.Success
            ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListMyInvitations(ICurrentUser currentUser, HouseService service, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        return Results.Ok(await service.ListInvitationsAsync(userId, ct));
    }

    private static async Task<IResult> AcceptInvitation(Guid invitationId, ICurrentUser currentUser,
        HouseService service, HouseSessionService sessions, HttpContext context, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.AcceptInvitationAsync(userId, invitationId, ct);
        if (result.Status == HouseOperationStatus.NotFound) return Results.NotFound();
        if (result.Status == HouseOperationStatus.UserNotFound) return Results.Unauthorized();
        var token = result.ActivateHouse ? await sessions.ActivateAsync(userId, result.ActiveHouseId, context, ct) : null;
        return Results.Ok(new HouseWithTokenResponse(result.House!, token));
    }

    private static async Task<IResult> DeclineInvitation(Guid invitationId, ICurrentUser currentUser,
        HouseService service, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        return await service.DeclineInvitationAsync(userId, invitationId, ct) is HouseOperationStatus.Success
            ? Results.NoContent() : Results.NotFound();
    }
}

public sealed record DashboardStatsResponse(int RecipeCount, int PantryItemCount);
