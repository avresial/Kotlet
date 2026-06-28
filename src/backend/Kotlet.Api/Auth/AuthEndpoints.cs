using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Kotlet.Domain.Houses;

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
        auth.MapGet("/house", House).RequireAuthorization();
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
        var user = new User { Id = Guid.NewGuid(), HouseId = DefaultHouse.Id, Email = email, NormalizedEmail = normalized, PasswordHash = "", DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(), CreatedAtUtc = now, UpdatedAtUtc = now };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try { await IssueTokens(user, db, tokens, context, environment, cancellationToken); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "An account with this email already exists." }); }
        return Results.Created("/api/auth/me", Response(user, tokens));
    }

    private static async Task<IResult> Login(LoginRequest request, KotletDbContext db, IPasswordHasher<User> hasher,
        TokenService tokens, HttpContext context, IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Email and password are required."] });
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == NormalizeEmail(request.Email), cancellationToken);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Results.Unauthorized();
        user.LastLoginAtUtc = user.UpdatedAtUtc = DateTime.UtcNow;
        await IssueTokens(user, db, tokens, context, environment, cancellationToken);
        return Results.Ok(Response(user, tokens));
    }

    private static async Task<IResult> Refresh(KotletDbContext db, TokenService tokens, HttpContext context,
        IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        var raw = tokens.ReadRefreshCookie(context.Request);
        if (string.IsNullOrEmpty(raw)) return Unauthorized(tokens, context, environment);
        var old = await db.RefreshTokens.Include(x => x.User).SingleOrDefaultAsync(x => x.TokenHash == tokens.Hash(raw), cancellationToken);
        if (old is null || old.ExpiresAtUtc <= DateTime.UtcNow) return Unauthorized(tokens, context, environment);
        if (old.RevokedAtUtc is not null)
        {
            await RevokeTokenFamily(old.UserId, db, cancellationToken);
            return Unauthorized(tokens, context, environment);
        }
        var (newRaw, replacement) = tokens.CreateRefreshToken(old.User, context);
        old.RevokedAtUtc = DateTime.UtcNow;
        old.ReplacedByTokenId = replacement.Id;
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        tokens.SetRefreshCookie(context.Response, newRaw, replacement.ExpiresAtUtc, IsSecure(environment));
        return Results.Ok(Response(old.User, tokens));
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
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return user is null ? Results.Unauthorized() : Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> House(ICurrentUser currentUser, KotletDbContext db, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var house = await db.Houses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == houseId, cancellationToken);
        if (house is null) return Results.Unauthorized();
        var members = await db.Users.AsNoTracking()
            .Where(x => x.HouseId == houseId)
            .OrderByDescending(x => x.Id == userId)
            .ThenBy(x => x.DisplayName ?? x.Email)
            .Select(x => new HouseMemberResponse(x.Id, x.Email, x.DisplayName, x.LastLoginAtUtc, x.Id == userId))
            .ToListAsync(cancellationToken);
        return Results.Ok(new HouseResponse(house.Id, house.Name, members));
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
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return Results.Unauthorized();
        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName;
        user.PreferredLanguage = preferredLanguage;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(user));
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

    private static async Task IssueTokens(User user, KotletDbContext db, TokenService tokens, HttpContext context, IWebHostEnvironment environment, CancellationToken ct)
    {
        var (raw, refresh) = tokens.CreateRefreshToken(user, context);
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);
        tokens.SetRefreshCookie(context.Response, raw, refresh.ExpiresAtUtc, IsSecure(environment));
    }

    private static AuthResponse Response(User user, TokenService tokens) { var access = tokens.CreateAccessToken(user); return new(ToResponse(user), access.Token, access.ExpiresAtUtc); }
    private static bool IsSecure(IWebHostEnvironment env) => !env.IsDevelopment() && !env.IsEnvironment("Test");
    private static IResult Unauthorized(TokenService tokens, HttpContext context, IWebHostEnvironment env) { tokens.ClearRefreshCookie(context.Response, IsSecure(env)); return Results.Unauthorized(); }
    private static async Task RevokeTokenFamily(Guid userId, KotletDbContext db, CancellationToken ct) { var active = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAtUtc == null).ToListAsync(ct); var now = DateTime.UtcNow; active.ForEach(x => x.RevokedAtUtc = now); await db.SaveChangesAsync(ct); }
    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    private static CurrentUserResponse ToResponse(User user) => new(user.Id, user.Email, user.DisplayName, user.PreferredLanguage, user.CreatedAtUtc, user.LastLoginAtUtc);
    private static Dictionary<string, string[]> ValidateRegistration(RegisterRequest r) { var e = new Dictionary<string, string[]>(); if (string.IsNullOrWhiteSpace(r.Email) || !System.Net.Mail.MailAddress.TryCreate(r.Email.Trim(), out _)) e["email"] = ["A valid email is required."]; if (string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 8) e["password"] = ["Password must be at least 8 characters long."]; if (r.Password != r.ConfirmPassword) e["confirmPassword"] = ["Passwords do not match."]; if (r.DisplayName?.Trim().Length > 100) e["displayName"] = ["Display name cannot exceed 100 characters."]; return e; }
}
