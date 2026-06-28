using Kotlet.Application.Ai;
using Kotlet.Domain.Ai;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Ai;

internal sealed class UserAiProviderRepository(KotletDbContext dbContext) : IUserAiProviderRepository
{
    public Task<UserAiProviderConfiguration?> GetAsync(Guid userId, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<UserAiProviderConfiguration> query = dbContext.UserAiProviderConfigurations;
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public void Add(UserAiProviderConfiguration configuration) => dbContext.UserAiProviderConfigurations.Add(configuration);
    public void Remove(UserAiProviderConfiguration configuration) => dbContext.UserAiProviderConfigurations.Remove(configuration);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
