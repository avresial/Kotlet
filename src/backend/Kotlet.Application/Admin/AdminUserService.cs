using System.Net.Mail;
using Kotlet.Domain.Auth;

namespace Kotlet.Application.Admin;

public sealed class AdminUserService(IAdminUserRepository repository)
{
    private const int PageSize = 10;

    public async Task<AdminUserPageResponse> GetUsersAsync(int page, string? search, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        var (users, totalCount) = await repository.GetPagedAsync(page, PageSize, search, cancellationToken);
        return new(users.Select(ToResponse).ToList(), page, PageSize, totalCount);
    }

    public async Task<AdminUserOperationResult> UpdateAsync(
        Guid id, Guid currentUserId, UpdateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return new(AdminUserOperationStatus.ValidationFailed, ValidationErrors: errors);

        var user = await repository.GetByIdAsync(id, cancellationToken);
        if (user is null) return new(AdminUserOperationStatus.NotFound);

        var availableRoles = await repository.GetRolesAsync(cancellationToken);
        var requestedRoles = request.Roles!.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (requestedRoles.Any(role => !availableRoles.ContainsKey(role)))
            return Validation("roles", "One or more roles are invalid.");
        if (currentUserId == id && user.Roles.Any(role => role.Name == RoleNames.Admin) &&
            !requestedRoles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase))
            return Validation("roles", "You cannot remove your own Admin role.");

        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        if (await repository.EmailExistsAsync(id, normalizedEmail, cancellationToken))
            return new(AdminUserOperationStatus.Conflict);

        user.Email = email;
        user.NormalizedEmail = normalizedEmail;
        user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email.Split('@', 2)[0] : request.DisplayName.Trim();
        user.PreferredLanguage = request.PreferredLanguage?.Trim().ToLowerInvariant();
        foreach (var role in user.Roles.Where(role => !requestedRoles.Contains(role.Name, StringComparer.OrdinalIgnoreCase)).ToArray())
            user.Roles.Remove(role);
        foreach (var roleName in requestedRoles.Where(roleName => user.Roles.All(role => !role.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase))))
            user.Roles.Add(availableRoles[roleName]);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return new(AdminUserOperationStatus.Success, ToResponse(user));
    }

    public async Task<AdminUserOperationStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(id, cancellationToken);
        if (user is null) return AdminUserOperationStatus.NotFound;
        repository.Remove(user);
        await repository.SaveChangesAsync(cancellationToken);
        return AdminUserOperationStatus.Success;
    }

    private static Dictionary<string, string[]> Validate(UpdateAdminUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email) || !MailAddress.TryCreate(request.Email.Trim(), out _))
            errors["email"] = ["A valid email is required."];
        if (request.DisplayName?.Trim().Length > 100)
            errors["displayName"] = ["Display name cannot exceed 100 characters."];
        if (request.PreferredLanguage?.Trim().ToLowerInvariant() is not null and not ("en" or "pl"))
            errors["preferredLanguage"] = ["Preferred language must be 'en' or 'pl'."];
        if (request.Roles is null)
            errors["roles"] = ["Roles are required."];
        return errors;
    }

    private static AdminUserOperationResult Validation(string field, string message) =>
        new(AdminUserOperationStatus.ValidationFailed, ValidationErrors: new() { [field] = [message] });

    private static AdminUserResponse ToResponse(User user) => new(user.Id, user.Email, user.DisplayName,
        user.PreferredLanguage, user.CreatedAtUtc, user.UpdatedAtUtc, user.LastLoginAtUtc,
        user.Roles.OrderBy(role => role.Name).Select(role => role.Name).ToArray());
}
