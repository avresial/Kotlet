using Kotlet.Domain.Ai;

namespace Kotlet.Application.Ai;

public sealed record AiProviderConfigurationDto(
    string ProviderName, string BaseUrl, string? DefaultModel, IReadOnlyList<string> Models, bool IsEnabled, bool HasApiKey,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record SaveAiProviderConfigurationCommand(
    string? ProviderName, string? BaseUrl, string? DefaultModel, bool IsEnabled, string? ApiKey, IReadOnlyList<string>? Models = null);

public sealed record AiProviderOperationResult(
    AiProviderConfigurationDto? Configuration = null, Dictionary<string, string[]>? ValidationErrors = null);

public sealed class UserAiProviderService(IUserAiProviderRepository repository)
{
    public async Task<AiProviderConfigurationDto?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        ToDto(await repository.GetAsync(userId, false, cancellationToken));

    public async Task<AiProviderOperationResult> SaveAsync(
        Guid userId, SaveAiProviderConfigurationCommand command, CancellationToken cancellationToken)
    {
        var existing = await repository.GetAsync(userId, true, cancellationToken);
        var errors = Validate(command, existing?.ApiKey);
        if (errors.Count > 0)
            return new(ValidationErrors: errors);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            // ponytail: Raw-key access stays in this service; add encryption here when protected key storage is configured.
            existing = new UserAiProviderConfiguration
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProviderName = command.ProviderName!.Trim(),
                BaseUrl = command.BaseUrl?.Trim() ?? "",
                ApiKey = NormalizeKey(command.ApiKey),
                DefaultModel = NullIfWhiteSpace(command.DefaultModel),
                Models = NormalizeModels(command),
                IsEnabled = command.IsEnabled,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            repository.Add(existing);
        }
        else
        {
            existing.ProviderName = command.ProviderName!.Trim();
            existing.BaseUrl = command.BaseUrl?.Trim() ?? "";
            existing.DefaultModel = NullIfWhiteSpace(command.DefaultModel);
            existing.Models = NormalizeModels(command);
            existing.IsEnabled = command.IsEnabled;
            existing.UpdatedAtUtc = now;
            if (!string.IsNullOrWhiteSpace(command.ApiKey))
                existing.ApiKey = command.ApiKey;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return new(ToDto(existing));
    }

    public async Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var configuration = await repository.GetAsync(userId, true, cancellationToken);
        if (configuration is null)
            return false;
        repository.Remove(configuration);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static Dictionary<string, string[]> Validate(SaveAiProviderConfigurationCommand command, string? existingKey)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.ProviderName))
            errors["providerName"] = ["Provider name is required."];
        else if (command.ProviderName.Trim().Length > 100)
            errors["providerName"] = ["Provider name cannot exceed 100 characters."];

        var baseUrl = command.BaseUrl?.Trim();
        if (baseUrl?.Length > 2048)
            errors["baseUrl"] = ["Base URL cannot exceed 2048 characters."];
        else if (command.IsEnabled && string.IsNullOrWhiteSpace(baseUrl))
            errors["baseUrl"] = ["Base URL is required when enabled."];
        else if (!string.IsNullOrWhiteSpace(baseUrl) &&
                 (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            errors["baseUrl"] = ["Base URL must be an absolute HTTP or HTTPS URL."];

        if (command.DefaultModel?.Trim().Length > 200)
            errors["defaultModel"] = ["Default model cannot exceed 200 characters."];
        if (command.Models?.Any(x => x.Trim().Length > 200) == true)
            errors["models"] = ["Model names cannot exceed 200 characters."];
        if (command.Models?.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).Count() > 20)
            errors["models"] = ["At most 20 models are allowed."];
        if (NormalizeModels(command)?.Length > 2000)
            errors["models"] = ["Models cannot exceed 2000 characters in total."];
        if (command.ApiKey?.Length > 4096)
            errors["apiKey"] = ["API key cannot exceed 4096 characters."];
        else if (command.IsEnabled && string.IsNullOrWhiteSpace(command.ApiKey) && string.IsNullOrWhiteSpace(existingKey))
            errors["apiKey"] = ["API key is required when enabled."];
        return errors;
    }

    private static AiProviderConfigurationDto? ToDto(UserAiProviderConfiguration? configuration) => configuration is null ? null : new(
        configuration.ProviderName, configuration.BaseUrl, configuration.DefaultModel, ParseModels(configuration), configuration.IsEnabled,
        !string.IsNullOrEmpty(configuration.ApiKey), configuration.CreatedAtUtc, configuration.UpdatedAtUtc);

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeKey(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string? NormalizeModels(SaveAiProviderConfigurationCommand command) => string.Join('\n',
        (command.Models ?? []).Append(command.DefaultModel ?? "").Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal));
    public static IReadOnlyList<string> ParseModels(UserAiProviderConfiguration configuration) =>
        (configuration.Models ?? configuration.DefaultModel ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
