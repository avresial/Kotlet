using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Auth");
        auth.MapPost("/register", Register);
        auth.MapPost("/login", Login);
        auth.MapPost("/refresh", Refresh);
        auth.MapPost("/logout", Logout);
        auth.MapGet("/me", Me).RequireAuthorization();
        auth.MapPut("/profile", UpdateProfile).RequireAuthorization();
        auth.MapPost("/password", ChangePassword).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> Register(RegisterRequest request, KotletDbContext db, IPasswordHasher<User> hasher,
        TokenService tokens, HttpContext context, IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        var errors = ValidateRegistration(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var email = request.Email.Trim();
        var normalized = NormalizeEmail(email);
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalized, cancellationToken))
            return Results.Conflict(new { message = "An account with this email already exists." });
        var now = DateTime.UtcNow;
        // New users start without a home; they set one up on first login.
        var userRole = await db.Roles.SingleAsync(role => role.Name == RoleNames.User, cancellationToken);
        var user = new User { Id = Guid.NewGuid(), Email = email, NormalizedEmail = normalized, PasswordHash = "", DisplayName = ResolveDisplayName(request.DisplayName, email), CreatedAtUtc = now, UpdatedAtUtc = now, Roles = [userRole] };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try { await IssueTokens(user, null, db, tokens, context, environment, cancellationToken); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "An account with this email already exists." }); }
        return Results.Created("/api/auth/me", Response(user, null, false, tokens));
    }

    private static async Task<IResult> Login(LoginRequest request, KotletDbContext db, IPasswordHasher<User> hasher,
        TokenService tokens, HttpContext context, IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Email and password are required."] });
        var user = await db.Users.Include(x => x.Roles).SingleOrDefaultAsync(x => x.NormalizedEmail == NormalizeEmail(request.Email), cancellationToken);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Results.Unauthorized();
        user.LastLoginAtUtc = user.UpdatedAtUtc = DateTime.UtcNow;
        var active = await ResolveActiveHouseAsync(user, db, cancellationToken);
        await IssueTokens(user, active, db, tokens, context, environment, cancellationToken);
        return Results.Ok(Response(user, active, active.HasValue, tokens));
    }

    private static async Task<IResult> Refresh(KotletDbContext db, TokenService tokens, HttpContext context,
        IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        var raw = tokens.ReadRefreshCookie(context.Request);
        if (string.IsNullOrEmpty(raw)) return Unauthorized(tokens, context, environment);
        var old = await db.RefreshTokens.Include(x => x.User).ThenInclude(x => x.Roles).SingleOrDefaultAsync(x => x.TokenHash == tokens.Hash(raw), cancellationToken);
        if (old is null || old.ExpiresAtUtc <= DateTime.UtcNow) return Unauthorized(tokens, context, environment);
        if (old.RevokedAtUtc is not null)
        {
            await RevokeTokenFamily(old.UserId, db, cancellationToken);
            return Unauthorized(tokens, context, environment);
        }
        // Carry the active home from the rotated token so a session switch survives silent refreshes,
        // but drop it if that home is no longer one the user belongs to.
        var active = old.HouseId is { } houseId && await db.HouseMemberships.AnyAsync(m => m.UserId == old.UserId && m.HouseId == houseId, cancellationToken)
            ? old.HouseId
            : null;
        var (newRaw, replacement) = tokens.CreateRefreshToken(old.User, context, active);
        old.RevokedAtUtc = DateTime.UtcNow;
        old.ReplacedByTokenId = replacement.Id;
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        tokens.SetRefreshCookie(context.Response, newRaw, replacement.ExpiresAtUtc, IsSecure(environment));
        var hasHome = await db.HouseMemberships.AnyAsync(m => m.UserId == old.UserId, cancellationToken);
        return Results.Ok(Response(old.User, active, hasHome, tokens));
    }

    private static async Task<IResult> Logout(KotletDbContext db, TokenService tokens, HttpContext context,
        IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        var raw = tokens.ReadRefreshCookie(context.Request);
        if (!string.IsNullOrEmpty(raw))
        {
            var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokens.Hash(raw), cancellationToken);
            if (token is { RevokedAtUtc: null }) { token.RevokedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(cancellationToken); }
        }
        tokens.ClearRefreshCookie(context.Response, IsSecure(environment));
        return Results.NoContent();
    }

    private static async Task<IResult> Me(ICurrentUser currentUser, KotletDbContext db, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } id) return Results.Unauthorized();
        var user = await db.Users.AsNoTracking().Include(x => x.Roles).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return Results.Unauthorized();
        var hasHome = await db.HouseMemberships.AnyAsync(m => m.UserId == id, cancellationToken);
        return Results.Ok(ToResponse(user, currentUser.HouseId, hasHome));
    }

    private static async Task<IResult> UpdateProfile(UpdateProfileRequest request, ICurrentUser currentUser, KotletDbContext db, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } id) return Results.Unauthorized();
        var displayName = request.DisplayName?.Trim();
        if (displayName?.Length > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["displayName"] = ["Display name cannot exceed 100 characters."] });
        var preferredLanguage = request.PreferredLanguage?.Trim().ToLowerInvariant();
        if (preferredLanguage is not null and not ("en" or "pl"))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["preferredLanguage"] = ["Preferred language must be 'en' or 'pl'."] });
        var user = await db.Users.Include(x => x.Roles).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return Results.Unauthorized();
        if (request.DefaultHouseId is { } defaultHouseId)
        {
            if (!await db.HouseMemberships.AnyAsync(m => m.UserId == id && m.HouseId == defaultHouseId, cancellationToken))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["defaultHouseId"] = ["You are not a member of this home."] });
            user.DefaultHouseId = defaultHouseId;
        }
        user.DisplayName = ResolveDisplayName(displayName, user.Email);
        user.PreferredLanguage = preferredLanguage;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var hasHome = await db.HouseMemberships.AnyAsync(m => m.UserId == id, cancellationToken);
        return Results.Ok(ToResponse(user, currentUser.HouseId, hasHome));
    }

    private static async Task<IResult> ChangePassword(ChangePasswordRequest request, ICurrentUser currentUser, KotletDbContext db, IPasswordHasher<User> hasher, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } id) return Results.Unauthorized();
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.CurrentPassword)) errors["currentPassword"] = ["Current password is required."];
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8) errors["newPassword"] = ["Password must be at least 8 characters long."];
        if (request.NewPassword != request.ConfirmPassword) errors["confirmPassword"] = ["Passwords do not match."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return Results.Unauthorized();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["currentPassword"] = ["Current password is incorrect."] });
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<Guid?> ResolveActiveHouseAsync(User user, KotletDbContext db, CancellationToken ct)
    {
        if (user.DefaultHouseId is { } d && await db.HouseMemberships.AnyAsync(m => m.UserId == user.Id && m.HouseId == d, ct))
            return d;
        return await db.HouseMemberships.Where(m => m.UserId == user.Id)
            .OrderBy(m => m.JoinedAtUtc).Select(m => (Guid?)m.HouseId).FirstOrDefaultAsync(ct);
    }

    private static async Task IssueTokens(User user, Guid? activeHouseId, KotletDbContext db, TokenService tokens, HttpContext context, IWebHostEnvironment environment, CancellationToken ct)
    {
        var (raw, refresh) = tokens.CreateRefreshToken(user, context, activeHouseId);
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);
        tokens.SetRefreshCookie(context.Response, raw, refresh.ExpiresAtUtc, IsSecure(environment));
    }

    private static AuthResponse Response(User user, Guid? activeHouseId, bool hasHome, TokenService tokens) { var access = tokens.CreateAccessToken(user, activeHouseId); return new(ToResponse(user, activeHouseId, hasHome), access.Token, access.ExpiresAtUtc); }
    private static bool IsSecure(IWebHostEnvironment env) => !env.IsDevelopment() && !env.IsEnvironment("Test");
    private static IResult Unauthorized(TokenService tokens, HttpContext context, IWebHostEnvironment env) { tokens.ClearRefreshCookie(context.Response, IsSecure(env)); return Results.Unauthorized(); }
    private static async Task RevokeTokenFamily(Guid userId, KotletDbContext db, CancellationToken ct) { var active = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAtUtc == null).ToListAsync(ct); var now = DateTime.UtcNow; active.ForEach(x => x.RevokedAtUtc = now); await db.SaveChangesAsync(ct); }
    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    private static CurrentUserResponse ToResponse(User user, Guid? activeHouseId, bool hasHome) => new(user.Id, user.Email, ResolveDisplayName(user.DisplayName, user.Email), user.PreferredLanguage, user.CreatedAtUtc, user.LastLoginAtUtc, user.DefaultHouseId, activeHouseId, hasHome, user.Roles.Select(role => role.Name).ToArray());
    private static string ResolveDisplayName(string? displayName, string email) =>
        string.IsNullOrWhiteSpace(displayName) ? email.Split('@', 2)[0] : displayName.Trim();
    private static Dictionary<string, string[]> ValidateRegistration(RegisterRequest r) { var e = new Dictionary<string, string[]>(); if (string.IsNullOrWhiteSpace(r.Email) || !System.Net.Mail.MailAddress.TryCreate(r.Email.Trim(), out _)) e["email"] = ["A valid email is required."]; if (string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 8) e["password"] = ["Password must be at least 8 characters long."]; if (r.Password != r.ConfirmPassword) e["confirmPassword"] = ["Passwords do not match."]; if (r.DisplayName?.Trim().Length > 100) e["displayName"] = ["Display name cannot exceed 100 characters."]; return e; }
}
