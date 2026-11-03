using System.Security.Claims;
using Kotlet.Application.Auth;
using Microsoft.Extensions.Options;

namespace Kotlet.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints
            .MapGroup("/api/auth")
            .WithTags("Auth");

        auth.MapPost("/register", Register);

        auth.MapPost("/login", Login);

        auth.MapPost("/oauth-bridge", OAuthBridge).DisableAntiforgery();

        auth.MapPost("/refresh", Refresh);

        auth.MapPost("/logout", Logout);

        auth.MapGet("/me", Me).RequireAuthorization();

        auth.MapPut("/profile", UpdateProfile).RequireAuthorization();

        auth.MapPost("/password", ChangePassword).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> Register(RegisterRequest request, AccountService accounts,
        AuthSessionService authSessions, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await accounts.RegisterAsync(request, cancellationToken);
        if (result.Status == AccountOperationStatus.ValidationFailed)
        {
            return Results.ValidationProblem(result.ValidationErrors!);
        }
        if (result.Status == AccountOperationStatus.Conflict)
        {
            return Results.Conflict(new { message = "An account with this email already exists." });
        }
        var response = await authSessions.TrySignInNewUserAsync(result.User!, context, cancellationToken);
        if (response is null)
        {
            return Results.Conflict(new { message = "An account with this email already exists." });
        }
        return Results.Created("/api/auth/me", response);
    }

    private static async Task<IResult> Login(LoginRequest request, AccountService accounts,
        AuthSessionService authSessions, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await accounts.LoginAsync(request, cancellationToken);
        if (result.Status == AccountOperationStatus.ValidationFailed)
        {
            return Results.ValidationProblem(result.ValidationErrors!);
        }
        if (result.Status == AccountOperationStatus.Unauthorized)
        {
            return Results.Unauthorized();
        }
        return Results.Ok(await authSessions.SignInAsync(
            result.User!, result.ActiveHouseId, result.HasHouse, context, cancellationToken));
    }

    /// <summary>
    /// Establishes the OAuth authorization session as a first-party cookie on the API origin.
    /// The SPA login page lives on a different site (GitHub Pages) than the authorization
    /// endpoint (the API), so the refresh cookie set by the cross-site <c>login</c> fetch is a
    /// third-party cookie that mobile browsers block. After a successful login the SPA performs a
    /// top-level POST here with the freshly issued access token; because that navigation is
    /// first-party to the API origin, the cookie set below is stored even on mobile. The endpoint
    /// then redirects back to <c>/connect/authorize</c>, which now finds the session.
    /// </summary>
    private static async Task<IResult> OAuthBridge(HttpRequest request, AuthSessionService authSessions,
        TokenService tokens, IOptions<OAuthOptions> oauthOptions, HttpContext context,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var returnUrl = form["returnUrl"].ToString();
        if (!IsValidAuthorizeReturnUrl(returnUrl, oauthOptions.Value))
        {
            return Results.BadRequest(new { message = "The return URL is invalid." });
        }

        var principal = tokens.ValidateAccessToken(form["token"].ToString());
        if (principal is null || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Results.Unauthorized();
        }

        var activeHouseId = Guid.TryParse(principal.FindFirstValue(KotletClaimTypes.HouseId), out var houseId)
            ? houseId
            : (Guid?)null;
        if (!await authSessions.StartSessionAsync(userId, activeHouseId, context, cancellationToken))
        {
            return Results.Unauthorized();
        }
        return Results.Redirect(returnUrl);
    }

    private static async Task<IResult> Refresh(AuthSessionService authSessions, HttpContext context,
        CancellationToken cancellationToken)
    {
        var response = await authSessions.RefreshAsync(context, cancellationToken);
        return response is null ? Results.Unauthorized() : Results.Ok(response);
    }

    private static async Task<IResult> Logout(AuthSessionService authSessions, HttpContext context,
        CancellationToken cancellationToken)
    {
        await authSessions.SignOutAsync(context, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> Me(ICurrentUser currentUser, AccountService accounts, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }
        var result = await accounts.GetAsync(userId, currentUser.HouseId, cancellationToken);
        return result.Status == AccountOperationStatus.Success
            ? Results.Ok(CurrentUserResponse.From(result.User!, result.ActiveHouseId, result.HasHouse))
            : Results.Unauthorized();
    }

    private static async Task<IResult> UpdateProfile(UpdateProfileRequest request, ICurrentUser currentUser,
        AccountService accounts, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }
        var result = await accounts.UpdateProfileAsync(userId, currentUser.HouseId, request, cancellationToken);
        if (result.Status == AccountOperationStatus.ValidationFailed)
        {
            return Results.ValidationProblem(result.ValidationErrors!);
        }
        return result.Status == AccountOperationStatus.Success
            ? Results.Ok(CurrentUserResponse.From(result.User!, result.ActiveHouseId, result.HasHouse))
            : Results.Unauthorized();
    }

    private static async Task<IResult> ChangePassword(ChangePasswordRequest request, ICurrentUser currentUser,
        AccountService accounts, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }
        var result = await accounts.ChangePasswordAsync(userId, request, cancellationToken);
        if (result.Status == AccountOperationStatus.ValidationFailed)
        {
            return Results.ValidationProblem(result.ValidationErrors!);
        }
        return result.Status == AccountOperationStatus.Success ? Results.NoContent() : Results.Unauthorized();
    }

    // The bridge only ever hands control back to the authorization endpoint on the API's own
    // origin; anything else (a foreign host or a different path) is rejected so the endpoint can
    // never be turned into an open redirect.
    private static bool IsValidAuthorizeReturnUrl(string returnUrl, OAuthOptions oauth) =>
        Uri.TryCreate(returnUrl, UriKind.Absolute, out var target) &&
        Uri.TryCreate(oauth.Issuer, UriKind.Absolute, out var issuer) &&
        string.Equals(target.GetLeftPart(UriPartial.Authority),
            issuer.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(target.AbsolutePath, "/connect/authorize", StringComparison.Ordinal);
}
