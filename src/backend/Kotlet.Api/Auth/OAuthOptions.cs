namespace Kotlet.Api.Auth;

public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";
    public required string Issuer { get; init; }
    public required string Resource { get; init; }
    public required string LoginUrl { get; init; }
    public string ClientId { get; init; } = "kotlet-mcp-dev";
    public bool RequirePkce { get; init; } = true;
    public string[] RedirectUris { get; init; } = [];
}
