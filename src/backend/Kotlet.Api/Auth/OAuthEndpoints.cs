using System.Security.Claims;
using Kotlet.Application.Auth;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Kotlet.Api.Auth;

public static class OAuthEndpoints
{
    public static IEndpointRouteBuilder MapOAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], Authorize);
        return endpoints;
    }

    private static async Task<IResult> Authorize(
        HttpContext context,
        IAuthRepository repository,
        TokenService tokens,
        IOptions<OAuthOptions> options,
        CancellationToken cancellationToken)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OAuth request is unavailable.");
        var resources = request.GetResources().ToArray();
        if (resources.Length != 1 || !string.Equals(resources[0], options.Value.Resource, StringComparison.Ordinal))
            return OAuthError(OpenIddictConstants.Errors.InvalidTarget, "The requested resource is invalid.");
        var resource = resources[0];

        var raw = tokens.ReadRefreshCookie(context.Request);
        var hash = raw is null ? null : tokens.Hash(raw);
        var session = hash is null
            ? null
            : await repository.GetRefreshSessionAsync(hash, DateTime.UtcNow, cancellationToken);
        if (session is null)
            return Results.Redirect(QueryHelpers.AddQueryString(
                options.Value.LoginUrl,
                "returnUrl",
                context.Request.GetEncodedUrl()));

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        identity.AddClaim(OpenIddictConstants.Claims.Subject, session.UserId.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, session.DisplayName ?? session.Email);
        identity.AddClaim(OpenIddictConstants.Claims.Email, session.Email);
        if (session.HouseId is { } houseId)
            identity.AddClaim(KotletClaimTypes.HouseId, houseId.ToString());
        foreach (var role in session.Roles)
            identity.AddClaim(OpenIddictConstants.Claims.Role, role);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(resource);
        principal.SetDestinations(_ => [OpenIddictConstants.Destinations.AccessToken]);

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IResult OAuthError(string error, string description) => Results.Forbid(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        }),
        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
