using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;
using OpenIddict.Abstractions;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.Auth;

/// <summary>MCP resource exposing the authenticated Kotlet identity.</summary>
[McpServerResourceType]
public sealed class IdentityMcp
{
    [McpServerResource(UriTemplate = "kotlet://identity", Name = "identity",
        Title = "Current Kotlet identity", MimeType = "application/json"),
     Description("Authenticated Kotlet user identity and roles.")]
    public static string Identity(ClaimsPrincipal user) => Json(new
    {
        UserId = user.FindFirstValue(OpenIddictConstants.Claims.Subject),
        Name = user.FindFirstValue(OpenIddictConstants.Claims.Name),
        Email = user.FindFirstValue(OpenIddictConstants.Claims.Email),
        Roles = user.FindAll(OpenIddictConstants.Claims.Role).Select(claim => claim.Value).ToArray()
    });
}
