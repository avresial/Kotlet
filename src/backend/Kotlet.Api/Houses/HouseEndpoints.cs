using Kotlet.Api.Auth;
using Kotlet.Domain.Auth;
using Kotlet.Domain.Houses;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Api.Houses;

public static class HouseEndpoints
{
    public static IEndpointRouteBuilder MapHouseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var houses = endpoints.MapGroup("/api/houses").WithTags("Houses").RequireAuthorization();
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

    private static async Task<IResult> ListHouses(ICurrentUser currentUser, KotletDbContext db, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var activeHouseId = currentUser.HouseId;
        var defaultHouseId = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DefaultHouseId).FirstOrDefaultAsync(ct);
        var houses = await db.HouseMemberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.House.Name)
            .Select(m => new HouseSummaryResponse(
                m.HouseId, m.House.Name, m.House.Memberships.Count,
                m.HouseId == defaultHouseId, m.HouseId == activeHouseId))
            .ToListAsync(ct);
        return Results.Ok(houses);
    }

    private static async Task<IResult> CreateHouse(CreateHouseRequest request, ICurrentUser currentUser, KotletDbContext db,
        TokenService tokens, HttpContext context, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        if (ValidateName(request.Name) is { } error) return error;
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Results.Unauthorized();
        var hadHome = await db.HouseMemberships.AnyAsync(m => m.UserId == userId, ct);
        var now = DateTime.UtcNow;
        var house = new House { Id = Guid.NewGuid(), Name = request.Name.Trim() };
        db.Houses.Add(house);
        db.HouseMemberships.Add(new HouseMembership { UserId = userId, HouseId = house.Id, JoinedAtUtc = now });
        if (!hadHome) user.DefaultHouseId = house.Id;
        await db.SaveChangesAsync(ct);
        var token = hadHome ? null : await ActivateHouseAsync(user, house.Id, db, tokens, context, ct);
        var summary = new HouseSummaryResponse(house.Id, house.Name, 1, user.DefaultHouseId == house.Id, !hadHome);
        return Results.Created($"/api/houses/{house.Id}", new HouseWithTokenResponse(summary, token));
    }

    private static async Task<IResult> GetHouse(Guid id, ICurrentUser currentUser, KotletDbContext db, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        if (!await IsMemberAsync(db, userId, id, ct)) return Results.NotFound();
        var house = await db.Houses.AsNoTracking().SingleOrDefaultAsync(h => h.Id == id, ct);
        if (house is null) return Results.NotFound();
        var members = await db.HouseMemberships.AsNoTracking()
            .Where(m => m.HouseId == id)
            .OrderByDescending(m => m.UserId == userId)
            .ThenBy(m => m.User.DisplayName ?? m.User.Email)
            .Select(m => new HouseMemberResponse(m.User.Id, m.User.Email, m.User.DisplayName, m.User.LastLoginAtUtc, m.User.Id == userId))
            .ToListAsync(ct);
        var pending = await db.HouseInvitations.AsNoTracking()
            .Where(i => i.HouseId == id)
            .OrderBy(i => i.InvitedUser.DisplayName ?? i.InvitedUser.Email)
            .Select(i => new PendingInvitationResponse(i.Id, i.InvitedUser.Email, i.InvitedUser.DisplayName, i.CreatedAtUtc))
            .ToListAsync(ct);
        return Results.Ok(new HouseDetailResponse(house.Id, house.Name, members, pending));
    }

    private static async Task<IResult> RenameHouse(Guid id, RenameHouseRequest request, ICurrentUser currentUser, KotletDbContext db, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        if (!await IsMemberAsync(db, userId, id, ct)) return Results.NotFound();
        if (ValidateName(request.Name) is { } error) return error;
        var house = await db.Houses.SingleOrDefaultAsync(h => h.Id == id, ct);
        if (house is null) return Results.NotFound();
        house.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteHouse(Guid id, ICurrentUser currentUser, KotletDbContext db,
        TokenService tokens, HttpContext context, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        if (!await IsMemberAsync(db, userId, id, ct)) return Results.NotFound();
        var house = await db.Houses.SingleOrDefaultAsync(h => h.Id == id, ct);
        if (house is null) return Results.NotFound();
        // Cascade removes everything scoped to this home: memberships, invitations, pantry and
        // shopping items, recipes (with their images and ingredients) and meal plans. Users keep
        // their accounts.
        db.Houses.Remove(house);
        await db.SaveChangesAsync(ct);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Results.NoContent();
        var newActive = await ResolveActiveHouseAsync(user, db, ct);
        var token = await ActivateHouseAsync(user, newActive, db, tokens, context, ct);
        return Results.Ok(token);
    }

    private static async Task<IResult> SwitchHouse(Guid id, ICurrentUser currentUser, KotletDbContext db,
        TokenService tokens, HttpContext context, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        if (!await IsMemberAsync(db, userId, id, ct)) return Results.NotFound();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Results.Unauthorized();
        var token = await ActivateHouseAsync(user, id, db, tokens, context, ct);
        return Results.Ok(token);
    }

    private static async Task<IResult> InviteMember(Guid id, InviteMemberRequest request, ICurrentUser currentUser, KotletDbContext db, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        if (!await IsMemberAsync(db, userId, id, ct)) return Results.NotFound();
        var email = request.Email?.Trim() ?? "";
        if (email.Length == 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["An email is required."] });
        var normalized = email.ToUpperInvariant();
        var invitee = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        if (invitee is null) return Results.NotFound(new { message = "No account exists for this email." });
        if (await db.HouseMemberships.AnyAsync(m => m.UserId == invitee.Id && m.HouseId == id, ct))
            return Results.Conflict(new { message = "This person is already a member." });
        if (await db.HouseInvitations.AnyAsync(i => i.HouseId == id && i.InvitedUserId == invitee.Id, ct))
            return Results.Conflict(new { message = "This person has already been invited." });
        var invitation = new HouseInvitation { Id = Guid.NewGuid(), HouseId = id, InvitedUserId = invitee.Id, InvitedByUserId = userId, CreatedAtUtc = DateTime.UtcNow };
        db.HouseInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new PendingInvitationResponse(invitation.Id, invitee.Email, invitee.DisplayName, invitation.CreatedAtUtc));
    }

    private static async Task<IResult> RemoveMember(Guid id, Guid memberUserId, ICurrentUser currentUser, KotletDbContext db, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        if (!await IsMemberAsync(db, userId, id, ct)) return Results.NotFound();
        var membership = await db.HouseMemberships.SingleOrDefaultAsync(m => m.HouseId == id && m.UserId == memberUserId, ct);
        if (membership is null) return Results.NotFound();
        db.HouseMemberships.Remove(membership);
        var removedUser = await db.Users.SingleOrDefaultAsync(u => u.Id == memberUserId, ct);
        if (removedUser is { DefaultHouseId: { } d } && d == id) removedUser.DefaultHouseId = null;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CancelInvitation(Guid id, Guid invitationId, ICurrentUser currentUser, KotletDbContext db, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        if (!await IsMemberAsync(db, userId, id, ct)) return Results.NotFound();
        var invitation = await db.HouseInvitations.SingleOrDefaultAsync(i => i.Id == invitationId && i.HouseId == id, ct);
        if (invitation is null) return Results.NotFound();
        db.HouseInvitations.Remove(invitation);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListMyInvitations(ICurrentUser currentUser, KotletDbContext db, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var invitations = await db.HouseInvitations.AsNoTracking()
            .Where(i => i.InvitedUserId == userId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new IncomingInvitationResponse(i.Id, i.HouseId, i.House.Name,
                i.InvitedByUser.DisplayName ?? i.InvitedByUser.Email, i.CreatedAtUtc))
            .ToListAsync(ct);
        return Results.Ok(invitations);
    }

    private static async Task<IResult> AcceptInvitation(Guid invitationId, ICurrentUser currentUser, KotletDbContext db,
        TokenService tokens, HttpContext context, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var invitation = await db.HouseInvitations.SingleOrDefaultAsync(i => i.Id == invitationId && i.InvitedUserId == userId, ct);
        if (invitation is null) return Results.NotFound();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Results.Unauthorized();
        var hadHome = await db.HouseMemberships.AnyAsync(m => m.UserId == userId, ct);
        var houseId = invitation.HouseId;
        var now = DateTime.UtcNow;
        if (!await db.HouseMemberships.AnyAsync(m => m.UserId == userId && m.HouseId == houseId, ct))
            db.HouseMemberships.Add(new HouseMembership { UserId = userId, HouseId = houseId, JoinedAtUtc = now });
        db.HouseInvitations.Remove(invitation);
        if (!hadHome) user.DefaultHouseId = houseId;
        await db.SaveChangesAsync(ct);
        var token = hadHome ? null : await ActivateHouseAsync(user, houseId, db, tokens, context, ct);
        var name = await db.Houses.AsNoTracking().Where(h => h.Id == houseId).Select(h => h.Name).FirstOrDefaultAsync(ct) ?? "";
        var memberCount = await db.HouseMemberships.CountAsync(m => m.HouseId == houseId, ct);
        var summary = new HouseSummaryResponse(houseId, name, memberCount, user.DefaultHouseId == houseId, !hadHome);
        return Results.Ok(new HouseWithTokenResponse(summary, token));
    }

    private static async Task<IResult> DeclineInvitation(Guid invitationId, ICurrentUser currentUser, KotletDbContext db, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var invitation = await db.HouseInvitations.SingleOrDefaultAsync(i => i.Id == invitationId && i.InvitedUserId == userId, ct);
        if (invitation is null) return Results.NotFound();
        db.HouseInvitations.Remove(invitation);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static Task<bool> IsMemberAsync(KotletDbContext db, Guid userId, Guid houseId, CancellationToken ct) =>
        db.HouseMemberships.AnyAsync(m => m.UserId == userId && m.HouseId == houseId, ct);

    private static async Task<Guid?> ResolveActiveHouseAsync(User user, KotletDbContext db, CancellationToken ct)
    {
        if (user.DefaultHouseId is { } d && await db.HouseMemberships.AnyAsync(m => m.UserId == user.Id && m.HouseId == d, ct))
            return d;
        return await db.HouseMemberships.Where(m => m.UserId == user.Id)
            .OrderBy(m => m.JoinedAtUtc).Select(m => (Guid?)m.HouseId).FirstOrDefaultAsync(ct);
    }

    // Makes <paramref name="houseId"/> the active home for the caller's current session: the active-home
    // pointer on the live refresh token is updated (so silent refreshes keep the choice) and a fresh
    // access token carrying the new house_id claim is returned.
    private static async Task<TokenResponse> ActivateHouseAsync(User user, Guid? houseId, KotletDbContext db,
        TokenService tokens, HttpContext context, CancellationToken ct)
    {
        var raw = tokens.ReadRefreshCookie(context.Request);
        if (!string.IsNullOrEmpty(raw))
        {
            var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokens.Hash(raw) && t.RevokedAtUtc == null, ct);
            if (token is not null) { token.HouseId = houseId; await db.SaveChangesAsync(ct); }
        }
        var access = tokens.CreateAccessToken(user, houseId);
        return new TokenResponse(access.Token, access.ExpiresAtUtc);
    }

    private static IResult? ValidateName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["A home name is required."] })
        : name.Trim().Length > 150 ? Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Home name cannot exceed 150 characters."] })
        : null;
}
