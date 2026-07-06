using Kotlet.Domain.Auth;
using Kotlet.Domain.Common;

namespace Kotlet.Application.Auth;

public sealed class AccountService(IAuthRepository repository, IUserPasswordService passwords)
{
    public async Task<AccountOperationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var errors = ValidateRegistration(request, out var email);
        if (errors.Count > 0) return Validation(errors);
        if (await repository.EmailExistsAsync(email.Normalized, cancellationToken))
            return new(AccountOperationStatus.Conflict);

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Value,
            NormalizedEmail = email.Normalized,
            PasswordHash = "",
            DisplayName = ResolveDisplayName(request.DisplayName, email.Value),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Roles = [await repository.GetRoleAsync(RoleNames.User, cancellationToken)]
        };
        user.PasswordHash = passwords.Hash(user, request.Password);
        repository.Add(user);
        return new(AccountOperationStatus.Success, user);
    }

    public async Task<AccountOperationResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Validation(new() { ["credentials"] = ["Email and password are required."] });
        var user = await repository.GetUserByNormalizedEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        if (user is null || !passwords.Verify(user, user.PasswordHash, request.Password))
            return new(AccountOperationStatus.Unauthorized);
        user.LastLoginAtUtc = user.UpdatedAtUtc = DateTime.UtcNow;
        var activeHouseId = await ResolveActiveHouseAsync(user, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new(AccountOperationStatus.Success, user, activeHouseId, activeHouseId.HasValue);
    }

    public async Task<AccountOperationResult> GetAsync(
        Guid userId, Guid? activeHouseId, CancellationToken cancellationToken)
    {
        var user = await repository.GetUserAsync(userId, cancellationToken);
        return user is null
            ? new(AccountOperationStatus.Unauthorized)
            : new(AccountOperationStatus.Success, user, activeHouseId,
                await repository.HasHouseAsync(userId, cancellationToken));
    }

    public async Task<AccountOperationResult> UpdateProfileAsync(
        Guid userId, Guid? activeHouseId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.DisplayName?.Trim().Length > 100) errors["displayName"] = ["Display name cannot exceed 100 characters."];
        var language = request.PreferredLanguage?.Trim().ToLowerInvariant();
        if (language is not null and not ("en" or "pl")) errors["preferredLanguage"] = ["Preferred language must be 'en' or 'pl'."];
        if (request.DefaultHouseId is { } defaultHouseId &&
            !await repository.IsMemberAsync(userId, defaultHouseId, cancellationToken))
            errors["defaultHouseId"] = ["You are not a member of this home."];
        if (errors.Count > 0) return Validation(errors);

        var user = await repository.GetUserAsync(userId, cancellationToken);
        if (user is null) return new(AccountOperationStatus.Unauthorized);
        user.DisplayName = ResolveDisplayName(request.DisplayName, user.Email);
        user.PreferredLanguage = language;
        if (request.DefaultHouseId.HasValue) user.DefaultHouseId = request.DefaultHouseId;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return new(AccountOperationStatus.Success, user, activeHouseId,
            await repository.HasHouseAsync(userId, cancellationToken));
    }

    public async Task<AccountOperationResult> ChangePasswordAsync(
        Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.CurrentPassword)) errors["currentPassword"] = ["Current password is required."];
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8) errors["newPassword"] = ["Password must be at least 8 characters long."];
        if (request.NewPassword != request.ConfirmPassword) errors["confirmPassword"] = ["Passwords do not match."];
        if (errors.Count > 0) return Validation(errors);
        var user = await repository.GetUserAsync(userId, cancellationToken);
        if (user is null) return new(AccountOperationStatus.Unauthorized);
        if (!passwords.Verify(user, user.PasswordHash, request.CurrentPassword))
            return Validation(new() { ["currentPassword"] = ["Current password is incorrect."] });
        user.PasswordHash = passwords.Hash(user, request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return new(AccountOperationStatus.Success);
    }

    private async Task<Guid?> ResolveActiveHouseAsync(User user, CancellationToken cancellationToken)
    {
        if (user.DefaultHouseId is { } defaultHouseId &&
            await repository.IsMemberAsync(user.Id, defaultHouseId, cancellationToken))
            return defaultHouseId;
        return await repository.GetFirstHouseIdAsync(user.Id, cancellationToken);
    }

    private static Dictionary<string, string[]> ValidateRegistration(RegisterRequest request, out Email email)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Email.TryCreate(request.Email, out email))
            errors["email"] = ["A valid email is required."];
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            errors["password"] = ["Password must be at least 8 characters long."];
        if (request.Password != request.ConfirmPassword) errors["confirmPassword"] = ["Passwords do not match."];
        if (request.DisplayName?.Trim().Length > 100) errors["displayName"] = ["Display name cannot exceed 100 characters."];
        return errors;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    private static string ResolveDisplayName(string? displayName, string email) =>
        string.IsNullOrWhiteSpace(displayName) ? email.Split('@', 2)[0] : displayName.Trim();
    private static AccountOperationResult Validation(Dictionary<string, string[]> errors) =>
        new(AccountOperationStatus.ValidationFailed, ValidationErrors: errors);
}
