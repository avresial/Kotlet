using Kotlet.Domain.Auth;

namespace Kotlet.Api.Auth;

public sealed record CurrentUserResponse(
    Guid Id, string Email, string? DisplayName, string? PreferredLanguage, string Theme, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc,
    Guid? DefaultHouseId, Guid? ActiveHouseId, bool HasHome, string[] Roles)
{
    public static CurrentUserResponse From(User user, Guid? activeHouseId, bool hasHome) => new(
        user.Id, user.Email, ResolveDisplayName(user.DisplayName, user.Email), user.PreferredLanguage, user.Theme,
        user.CreatedAtUtc, user.LastLoginAtUtc, user.DefaultHouseId, activeHouseId, hasHome,
        user.Roles.Select(role => role.Name).ToArray());

    private static string ResolveDisplayName(string? displayName, string email) =>
        string.IsNullOrWhiteSpace(displayName) ? email.Split('@', 2)[0] : displayName.Trim();
}
// RefreshToken travels in the body as well as in the Set-Cookie header: clients whose browser
// blocks the cross-site cookie (the SPA lives on a different site than the API) store it locally
// and present it back via the X-Refresh-Token header.
public sealed record AuthResponse(
    CurrentUserResponse User, string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken);
