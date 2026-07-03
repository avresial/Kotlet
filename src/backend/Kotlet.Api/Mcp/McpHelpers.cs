using System.Text.Json;
using Kotlet.Api.Auth;
using ModelContextProtocol.Protocol;

namespace Kotlet.Api.Mcp;

/// <summary>
/// Cross-cutting helpers shared by the per-domain MCP tool and resource classes
/// (which live alongside each feature's HTTP endpoints). Kept in one place so the
/// domain classes stay focused on their tools and resources.
/// </summary>
internal static class McpHelpers
{
    public static Guid RequireUser(ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("The authenticated user is unavailable.");

    public static Guid RequireHouse(ICurrentUser currentUser) =>
        currentUser.HouseId ?? throw new UnauthorizedAccessException(
            "No active household is available. Select a household in Kotlet and reconnect this MCP server.");

    public static ResourceLinkBlock Link(string uri, string title, string description) => new()
    {
        Uri = uri,
        Name = title,
        Title = title,
        Description = description,
        MimeType = "application/json"
    };

    public static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonSerializerOptions.Web);
}
