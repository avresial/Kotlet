using System.Text.Json.Serialization;

namespace Kotlet.Api.Mcp;

/// <summary>
/// Lightweight, client-friendly pointer to the hosted MCP server and its OAuth
/// endpoints. This complements — it does not replace — the standards-based
/// <c>/.well-known/openid-configuration</c> and
/// <c>/.well-known/oauth-protected-resource</c> documents.
/// </summary>
public sealed record McpDiscoveryDocument(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("mcp_endpoint")] string McpEndpoint,
    [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
    [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
    [property: JsonPropertyName("scopes_supported")] string[] ScopesSupported);
