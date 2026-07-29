namespace Kotlet.Api.Auth;

public sealed record CurrentUserResponse(
    Guid Id, string Email, string? DisplayName, string? PreferredLanguage, string Theme, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc,
    Guid? DefaultHouseId, Guid? ActiveHouseId, bool HasHome, string[] Roles);
// RefreshToken travels in the body as well as in the Set-Cookie header: clients whose browser
// blocks the cross-site cookie (the SPA lives on a different site than the API) store it locally
// and present it back via the X-Refresh-Token header.
public sealed record AuthResponse(
    CurrentUserResponse User, string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken);
