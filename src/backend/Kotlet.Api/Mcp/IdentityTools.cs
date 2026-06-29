using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;
using OpenIddict.Abstractions;

namespace Kotlet.Api.Mcp;

[McpServerToolType]
public sealed class IdentityTools
{
    [McpServerTool(Name = "who_am_i"), Description("Returns the authenticated Kotlet user's identity.")]
    public static object WhoAmI(ClaimsPrincipal user) => new
    {
        UserId = user.FindFirstValue(OpenIddictConstants.Claims.Subject),
        Name = user.FindFirstValue(OpenIddictConstants.Claims.Name),
        Email = user.FindFirstValue(OpenIddictConstants.Claims.Email),
        Roles = user.FindAll(OpenIddictConstants.Claims.Role).Select(claim => claim.Value).ToArray()
    };
}
