namespace Kotlet.Api.Auth;

public sealed record RegisterRequest(string Email, string Password, string ConfirmPassword, string? DisplayName = null);
public sealed record LoginRequest(string Email, string Password);
public sealed record UpdateProfileRequest(string? DisplayName, string? PreferredLanguage, Guid? DefaultHouseId = null);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record CurrentUserResponse(
    Guid Id, string Email, string? DisplayName, string? PreferredLanguage, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc,
    Guid? DefaultHouseId, Guid? ActiveHouseId, bool HasHome, string[] Roles);
public sealed record AuthResponse(CurrentUserResponse User, string AccessToken, DateTime AccessTokenExpiresAtUtc);
