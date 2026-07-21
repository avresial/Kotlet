using Kotlet.Api.Auth;
using Microsoft.Extensions.Options;

namespace Kotlet.Api.Mcp;

public static class McpEndpoints
{
    private const string ServerName = "Kotlet";
    private const string ServerDescription = "Kotlet household food MCP server";
    private const string SupportedScope = "mcp";

    public static IEndpointRouteBuilder MapMcpDiscovery(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/mcp.json", (IOptions<OAuthOptions> options) =>
        {
            var oauth = options.Value;
            var issuer = oauth.Issuer.TrimEnd('/');
            var document = new McpDiscoveryDocument(
                Name: ServerName,
                Version: ThisAssemblyVersion(),
                Description: ServerDescription,
                McpEndpoint: oauth.Resource,
                AuthorizationEndpoint: $"{issuer}/connect/authorize",
                TokenEndpoint: $"{issuer}/connect/token",
                ClientId: oauth.ClientId,
                ScopesSupported: [SupportedScope]);
            return Results.Ok(document);
        }).AllowAnonymous();
        return endpoints;
    }

    private static string ThisAssemblyVersion() =>
        typeof(McpEndpoints).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}
