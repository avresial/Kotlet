namespace Kotlet.Application.Admin;

public sealed record UpdateAdminUserRequest(string Email, string? DisplayName, string? PreferredLanguage, string[]? Roles);

public sealed record AdminUserResponse(Guid Id, string Email, string? DisplayName, string? PreferredLanguage,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? LastLoginAtUtc, string[] Roles);

public sealed record AdminUserPageResponse(IReadOnlyList<AdminUserResponse> Items, int Page, int PageSize, int TotalCount);

public enum AdminUserOperationStatus { Success, NotFound, Conflict, ValidationFailed }

public sealed record AdminUserOperationResult(
    AdminUserOperationStatus Status,
    AdminUserResponse? User = null,
    Dictionary<string, string[]>? ValidationErrors = null);
