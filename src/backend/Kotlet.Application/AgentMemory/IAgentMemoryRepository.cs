using Kotlet.Domain.AgentMemory;
using AgentMemoryEntity = Kotlet.Domain.AgentMemory.AgentMemory;

namespace Kotlet.Application.AgentMemory;

public interface IAgentMemoryRepository
{
    Task<IReadOnlyList<AgentMemoryEntity>> ListAsync(
        Guid userId, Guid houseId, string? query, string? category,
        AgentMemorySource? source, AgentMemoryReviewStatus? reviewStatus,
        bool includeExpired, CancellationToken cancellationToken);

    Task<AgentMemoryEntity?> GetAsync(Guid id, Guid userId, Guid houseId, bool includeDeleted, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentMemoryEntity>> GetChangesAsync(Guid userId, Guid houseId, long sinceRevision, CancellationToken cancellationToken);
    Task<long> GetRevisionAsync(Guid userId, Guid houseId, CancellationToken cancellationToken);
    Task<long> NextRevisionAsync(Guid userId, Guid houseId, CancellationToken cancellationToken);
    void Add(AgentMemoryEntity memory);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
