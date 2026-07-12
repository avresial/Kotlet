namespace Kotlet.Api.Auth;

public sealed record CurrentUserResponse(
    Guid Id, string Email, string? DisplayName, string? PreferredLanguage, string Theme, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc,
    Guid? DefaultHouseId, Guid? ActiveHouseId, bool HasHome, string[] Roles);
public sealed record AuthResponse(CurrentUserResponse User, string AccessToken, DateTime AccessTokenExpiresAtUtc);
