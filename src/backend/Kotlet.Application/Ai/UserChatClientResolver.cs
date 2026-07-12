using Microsoft.Extensions.AI;

namespace Kotlet.Application.Ai;

/// <summary>
/// Builds a chat client from the calling user's stored provider configuration. Model and key are
/// per user (see <see cref="Kotlet.Domain.Ai.UserAiProviderConfiguration"/>), so resolution happens
/// per request rather than through a single DI-registered client.
/// </summary>
internal sealed class UserChatClientResolver(
    IUserAiProviderRepository repository, IChatClientFactory factory) : IUserChatClientResolver
{
    public Task<IChatClient?> ResolveAsync(Guid userId, CancellationToken cancellationToken) =>
        ResolveAsync(userId, "", cancellationToken);

    public async Task<IChatClient?> ResolveAsync(Guid userId, string model, CancellationToken cancellationToken)
    {
        var configuration = await repository.GetAsync(userId, tracked: false, cancellationToken);
        if (configuration is null || !configuration.IsEnabled || string.IsNullOrWhiteSpace(configuration.ApiKey))
            return null;

        var models = UserAiProviderService.ParseModels(configuration);
        if (model.Length > 0 && !models.Contains(model, StringComparer.Ordinal)) return null;
        return factory.Create(new AiChatClientOptions(
            configuration.BaseUrl, configuration.ApiKey, model.Length > 0 ? model : configuration.DefaultModel ?? models.FirstOrDefault()));
    }
}
