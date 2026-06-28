using Kotlet.Domain.Ai;

namespace Kotlet.Application.Ai;

public interface IUserAiProviderRepository
{
    Task<UserAiProviderConfiguration?> GetAsync(Guid userId, bool tracked, CancellationToken cancellationToken);
    void Add(UserAiProviderConfiguration configuration);
    void Remove(UserAiProviderConfiguration configuration);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
