using Kotlet.Domain.Auth;

namespace Kotlet.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string ConfirmPassword, string? DisplayName = null);
public sealed record LoginRequest(string Email, string Password);
public sealed record UpdateProfileRequest(string? DisplayName, string? PreferredLanguage, Guid? DefaultHouseId = null);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record RefreshSession(Guid UserId, Guid? HouseId, string Email, string? DisplayName, string[] Roles);

public enum AccountOperationStatus { Success, Unauthorized, Conflict, ValidationFailed }

public sealed record AccountOperationResult(
    AccountOperationStatus Status,
    User? User = null,
    Guid? ActiveHouseId = null,
    bool HasHouse = false,
    Dictionary<string, string[]>? ValidationErrors = null);
