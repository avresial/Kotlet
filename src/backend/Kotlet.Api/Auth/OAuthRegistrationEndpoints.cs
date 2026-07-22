using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Kotlet.Api.Auth;

/// <summary>
/// RFC 7591 OpenID Connect / OAuth 2.0 Dynamic Client Registration.
/// <para>
/// ChatGPT connects with a single, pre-shared public client id, so a statically
/// seeded application is enough for it. Anthropic's clients (Claude Code, Claude
/// Desktop and the claude.ai web connector) instead follow the MCP authorization
/// spec: they read the authorization-server metadata, and if it advertises a
/// <c>registration_endpoint</c> they register a fresh public client that carries
/// <em>their own</em> redirect URI (a loopback <c>http://localhost:&lt;port&gt;/callback</c>
/// for the desktop/CLI clients, an HTTPS callback for the hosted web connector).
/// Without this endpoint those clients abort with "does not support dynamic client
/// registration", and any redirect they attempt is rejected because it was never
/// pre-registered — the "redirection issue" seen when wiring Claude up.
/// </para>
/// Registration is intentionally open (no initial access token): every client is
/// created as a public, PKCE-only application limited to the MCP scope and resource,
/// which is the trust model the MCP spec assumes.
/// </summary>
public static class OAuthRegistrationEndpoints
{
    public static IEndpointRouteBuilder MapOAuthRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/register", Register).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> Register(
        HttpContext context,
        IOpenIddictApplicationManager applications,
        IOptions<OAuthOptions> options,
        CancellationToken cancellationToken)
    {
        ClientRegistrationRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<ClientRegistrationRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return RegistrationError("invalid_client_metadata", "The registration request body is not valid JSON.");
        }

        if (request is null || request.RedirectUris is not { Length: > 0 })
            return RegistrationError("invalid_redirect_uri", "At least one redirect URI is required.");

        var redirectUris = new List<Uri>(request.RedirectUris.Length);
        foreach (var value in request.RedirectUris)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !IsAllowedRedirectUri(uri))
                return RegistrationError("invalid_redirect_uri", $"The redirect URI '{value}' is not allowed.");
            redirectUris.Add(uri);
        }

        var oauth = options.Value;
        var clientId = Guid.NewGuid().ToString("N");
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = string.IsNullOrWhiteSpace(request.ClientName) ? "MCP client" : request.ClientName
        };
        foreach (var uri in redirectUris)
            descriptor.RedirectUris.Add(uri);
        descriptor.Permissions.UnionWith([
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + "mcp",
            Permissions.Prefixes.Resource + oauth.Resource
        ]);
        // Public clients cannot keep a secret, so PKCE is always required for dynamically
        // registered clients regardless of the pre-shared client's RequirePkce setting.
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

        await applications.CreateAsync(descriptor, cancellationToken);

        var response = new ClientRegistrationResponse(
            ClientId: clientId,
            ClientIdIssuedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            RedirectUris: request.RedirectUris,
            TokenEndpointAuthMethod: "none",
            GrantTypes: ["authorization_code", "refresh_token"],
            ResponseTypes: ["code"],
            Scope: "mcp offline_access",
            ClientName: descriptor.DisplayName);
        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }

    private static bool IsAllowedRedirectUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps ||
        (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);

    private static IResult RegistrationError(string error, string description) => Results.Json(
        new Dictionary<string, string> { ["error"] = error, ["error_description"] = description },
        statusCode: StatusCodes.Status400BadRequest);

    private sealed record ClientRegistrationRequest(
        [property: JsonPropertyName("redirect_uris")] string[]? RedirectUris,
        [property: JsonPropertyName("client_name")] string? ClientName,
        [property: JsonPropertyName("grant_types")] string[]? GrantTypes,
        [property: JsonPropertyName("response_types")] string[]? ResponseTypes,
        [property: JsonPropertyName("token_endpoint_auth_method")] string? TokenEndpointAuthMethod,
        [property: JsonPropertyName("scope")] string? Scope);

    private sealed record ClientRegistrationResponse(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("client_id_issued_at")] long ClientIdIssuedAt,
        [property: JsonPropertyName("redirect_uris")] string[] RedirectUris,
        [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
        [property: JsonPropertyName("grant_types")] string[] GrantTypes,
        [property: JsonPropertyName("response_types")] string[] ResponseTypes,
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("client_name")] string? ClientName);
}
