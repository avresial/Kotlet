using Kotlet.Application.AgentMemory;
using Kotlet.Domain.AgentMemory;
using AgentMemoryEntity = Kotlet.Domain.AgentMemory.AgentMemory;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.AgentMemory;

internal sealed class AgentMemoryRepository(KotletDbContext dbContext) : IAgentMemoryRepository
{
    public async Task<IReadOnlyList<AgentMemoryEntity>> ListAsync(
        Guid userId, Guid houseId, string? query, string? category,
        AgentMemorySource? source, AgentMemoryReviewStatus? reviewStatus,
        bool includeExpired, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var memories = dbContext.AgentMemories.AsNoTracking()
            .Where(memory => memory.UserId == userId && memory.HouseId == houseId && !memory.IsDeleted);
        if (!includeExpired)
            memories = memories.Where(memory => memory.ExpiresAt == null || memory.ExpiresAt > now);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            memories = memories.Where(memory => (memory.Title != null && memory.Title.Contains(term)) || memory.Content.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(category)) memories = memories.Where(memory => memory.Category == category.Trim());
        if (source is { } sourceValue) memories = memories.Where(memory => memory.Source == sourceValue);
        if (reviewStatus is { } reviewValue) memories = memories.Where(memory => memory.ReviewStatus == reviewValue);
        return await memories.OrderByDescending(memory => memory.UpdatedAt).ThenBy(memory => memory.Id).ToListAsync(cancellationToken);
    }

    public Task<AgentMemoryEntity?> GetAsync(Guid id, Guid userId, Guid houseId, bool includeDeleted, CancellationToken cancellationToken) =>
        dbContext.AgentMemories.FirstOrDefaultAsync(memory => memory.Id == id && memory.UserId == userId &&
            memory.HouseId == houseId && (includeDeleted || !memory.IsDeleted), cancellationToken);

    public async Task<IReadOnlyList<AgentMemoryEntity>> GetChangesAsync(
        Guid userId, Guid houseId, long sinceRevision, CancellationToken cancellationToken) =>
        await dbContext.AgentMemories.AsNoTracking()
            .Where(memory => memory.UserId == userId && memory.HouseId == houseId && memory.Revision > sinceRevision)
            .OrderBy(memory => memory.Revision)
            .ToListAsync(cancellationToken);

    public async Task<long> GetRevisionAsync(Guid userId, Guid houseId, CancellationToken cancellationToken)
    {
        var revision = await dbContext.AgentMemoryCollections.AsNoTracking()
            .Where(collection => collection.UserId == userId && collection.HouseId == houseId)
            .Select(collection => (long?)collection.Revision)
            .SingleOrDefaultAsync(cancellationToken);
        return revision ?? 0;
    }

    public async Task<long> NextRevisionAsync(Guid userId, Guid houseId, CancellationToken cancellationToken)
    {
        var collection = await dbContext.AgentMemoryCollections.SingleOrDefaultAsync(
            item => item.UserId == userId && item.HouseId == houseId, cancellationToken);
        if (collection is null)
        {
            collection = new AgentMemoryCollection { UserId = userId, HouseId = houseId, Revision = 1 };
            dbContext.AgentMemoryCollections.Add(collection);
        }
        else
        {
            collection.Revision++;
        }
        return collection.Revision;
    }

    public void Add(AgentMemoryEntity memory) => dbContext.AgentMemories.Add(memory);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
