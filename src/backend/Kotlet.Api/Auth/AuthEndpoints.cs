using Kotlet.Application.Auth;
using Kotlet.Domain.Auth;

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

    private static async Task<IResult> Register(RegisterRequest request, AccountService accounts, IAuthSessionRepository sessions,
        TokenService tokens, HttpContext context, IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        var result = await accounts.RegisterAsync(request, cancellationToken);
        if (result.Status == AccountOperationStatus.ValidationFailed) return Results.ValidationProblem(result.ValidationErrors!);
        if (result.Status == AccountOperationStatus.Conflict)
            return Results.Conflict(new { message = "An account with this email already exists." });
        if (!await TryIssueTokens(result.User!, sessions, tokens, context, environment, cancellationToken))
            return Results.Conflict(new { message = "An account with this email already exists." });
        return Results.Created("/api/auth/me", Response(result.User!, null, false, tokens));
    }

    private static async Task<IResult> Login(LoginRequest request, AccountService accounts, IAuthSessionRepository sessions,
        TokenService tokens, HttpContext context, IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        var result = await accounts.LoginAsync(request, cancellationToken);
        if (result.Status == AccountOperationStatus.ValidationFailed) return Results.ValidationProblem(result.ValidationErrors!);
        if (result.Status == AccountOperationStatus.Unauthorized) return Results.Unauthorized();
        await IssueTokens(result.User!, result.ActiveHouseId, sessions, tokens, context, environment, cancellationToken);
        return Results.Ok(Response(result.User!, result.ActiveHouseId, result.HasHouse, tokens));
    }

    private static async Task<IResult> Refresh(IAuthSessionRepository sessions, TokenService tokens, HttpContext context,
        IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        var raw = tokens.ReadRefreshCookie(context.Request);
        if (string.IsNullOrEmpty(raw)) return Unauthorized(tokens, context, environment);
        var old = await sessions.GetRefreshTokenAsync(tokens.Hash(raw), cancellationToken);
        if (old is null || old.ExpiresAtUtc <= DateTime.UtcNow) return Unauthorized(tokens, context, environment);
        if (old.RevokedAtUtc is not null)
        {
            await sessions.RevokeFamilyAsync(old.UserId, DateTime.UtcNow, cancellationToken);
            return Unauthorized(tokens, context, environment);
        }
        // Carry the active home from the rotated token so a session switch survives silent refreshes,
        // but drop it if that home is no longer one the user belongs to.
        var active = old.HouseId is { } houseId && await sessions.IsMemberAsync(old.UserId, houseId, cancellationToken)
            ? old.HouseId
            : null;
        var (newRaw, replacement) = tokens.CreateRefreshToken(old.User, context, active);
        old.RevokedAtUtc = DateTime.UtcNow;
        old.ReplacedByTokenId = replacement.Id;
        sessions.Add(replacement);
        await sessions.SaveChangesAsync(cancellationToken);
        tokens.SetRefreshCookie(context.Response, newRaw, replacement.ExpiresAtUtc, IsSecure(environment));
        var hasHome = await sessions.HasHouseAsync(old.UserId, cancellationToken);
        return Results.Ok(Response(old.User, active, hasHome, tokens));
    }

    private static async Task<IResult> Logout(IAuthSessionRepository sessions, TokenService tokens, HttpContext context,
        IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        var raw = tokens.ReadRefreshCookie(context.Request);
        if (!string.IsNullOrEmpty(raw))
        {
            var token = await sessions.GetRefreshTokenAsync(tokens.Hash(raw), cancellationToken);
            if (token is { RevokedAtUtc: null })
            {
                token.RevokedAtUtc = DateTime.UtcNow;
                await sessions.SaveChangesAsync(cancellationToken);
            }
        }
        tokens.ClearRefreshCookie(context.Response, IsSecure(environment));
        return Results.NoContent();
    }

    private static async Task<IResult> Me(ICurrentUser currentUser, AccountService accounts, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await accounts.GetAsync(userId, currentUser.HouseId, cancellationToken);
        return result.Status == AccountOperationStatus.Success
            ? Results.Ok(ToResponse(result.User!, result.ActiveHouseId, result.HasHouse))
            : Results.Unauthorized();
    }

    private static async Task<IResult> UpdateProfile(UpdateProfileRequest request, ICurrentUser currentUser,
        AccountService accounts, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await accounts.UpdateProfileAsync(userId, currentUser.HouseId, request, cancellationToken);
        if (result.Status == AccountOperationStatus.ValidationFailed) return Results.ValidationProblem(result.ValidationErrors!);
        return result.Status == AccountOperationStatus.Success
            ? Results.Ok(ToResponse(result.User!, result.ActiveHouseId, result.HasHouse))
            : Results.Unauthorized();
    }

    private static async Task<IResult> ChangePassword(ChangePasswordRequest request, ICurrentUser currentUser,
        AccountService accounts, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await accounts.ChangePasswordAsync(userId, request, cancellationToken);
        if (result.Status == AccountOperationStatus.ValidationFailed) return Results.ValidationProblem(result.ValidationErrors!);
        return result.Status == AccountOperationStatus.Success ? Results.NoContent() : Results.Unauthorized();
    }

    private static async Task IssueTokens(User user, Guid? activeHouseId, IAuthSessionRepository sessions,
        TokenService tokens, HttpContext context, IWebHostEnvironment environment, CancellationToken ct)
    {
        var (raw, refresh) = tokens.CreateRefreshToken(user, context, activeHouseId);
        sessions.Add(refresh);
        await sessions.SaveChangesAsync(ct);
        tokens.SetRefreshCookie(context.Response, raw, refresh.ExpiresAtUtc, IsSecure(environment));
    }

    private static async Task<bool> TryIssueTokens(User user, IAuthSessionRepository sessions,
        TokenService tokens, HttpContext context, IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        var (raw, refreshToken) = tokens.CreateRefreshToken(user, context, null);
        sessions.Add(refreshToken);
        if (!await sessions.TrySaveChangesAsync(cancellationToken)) return false;
        tokens.SetRefreshCookie(context.Response, raw, refreshToken.ExpiresAtUtc, IsSecure(environment));
        return true;
    }

    private static AuthResponse Response(User user, Guid? activeHouseId, bool hasHome, TokenService tokens)
    {
        var accessToken = tokens.CreateAccessToken(user, activeHouseId);
        return new(ToResponse(user, activeHouseId, hasHome), accessToken.Token, accessToken.ExpiresAtUtc);
    }

    private static bool IsSecure(IWebHostEnvironment environment) =>
        !environment.IsDevelopment() && !environment.IsEnvironment("Test");

    private static IResult Unauthorized(TokenService tokens, HttpContext context, IWebHostEnvironment environment)
    {
        tokens.ClearRefreshCookie(context.Response, IsSecure(environment));
        return Results.Unauthorized();
    }

    private static CurrentUserResponse ToResponse(User user, Guid? activeHouseId, bool hasHome) => new(
        user.Id, user.Email, ResolveDisplayName(user.DisplayName, user.Email), user.PreferredLanguage,
        user.CreatedAtUtc, user.LastLoginAtUtc, user.DefaultHouseId, activeHouseId, hasHome,
        user.Roles.Select(role => role.Name).ToArray());

    private static string ResolveDisplayName(string? displayName, string email) =>
        string.IsNullOrWhiteSpace(displayName) ? email.Split('@', 2)[0] : displayName.Trim();
}
