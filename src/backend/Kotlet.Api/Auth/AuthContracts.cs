namespace Kotlet.Api.Auth;

public sealed record RegisterRequest(string Email, string Password, string ConfirmPassword, string? DisplayName = null);
public sealed record LoginRequest(string Email, string Password);
public sealed record UpdateProfileRequest(string? DisplayName);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record CurrentUserResponse(Guid Id, string Email, string? DisplayName, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc);
public sealed record AuthResponse(CurrentUserResponse User, string AccessToken, DateTime AccessTokenExpiresAtUtc);
public sealed record HouseMemberResponse(Guid Id, string Email, string? DisplayName, DateTime? LastLoginAtUtc, bool IsCurrentUser);
public sealed record HouseResponse(Guid Id, string Name, IReadOnlyList<HouseMemberResponse> Members);
