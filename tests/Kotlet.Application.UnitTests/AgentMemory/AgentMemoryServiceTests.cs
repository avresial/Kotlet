using AgentMemoryEntity = Kotlet.Domain.AgentMemory.AgentMemory;
using Kotlet.Application.AgentMemory;
using Kotlet.Domain.AgentMemory;
using Xunit;

namespace Kotlet.Application.UnitTests.AgentMemory;

public sealed class AgentMemoryServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid HouseId = Guid.NewGuid();

    [Fact]
    public async Task Create_UpdateAndList_AssignsRevisionsAndVersions()
    {
        var repository = new FakeRepository();
        var service = new AgentMemoryService(repository);

        var created = await service.CreateAsync(UserId, HouseId,
            new CreateAgentMemoryRequest("User dislikes mushrooms.", "Food", "food"), CancellationToken.None);
        var memory = Assert.IsType<AgentMemoryResponse>(created.Memory);

        Assert.Equal("Success", created.Status);
        Assert.Equal(1, memory.Version);
        Assert.Equal(1, memory.Revision);

        var updated = await service.UpdateAsync(memory.Id, UserId, HouseId,
            new UpdateAgentMemoryRequest(memory.Version, "User likes oyster mushrooms.", "Food", "food"), CancellationToken.None);

        Assert.Equal("Success", updated.Status);
        Assert.Equal(2, updated.Memory!.Version);
        Assert.Equal(2, updated.Memory.Revision);
        Assert.Equal("User likes oyster mushrooms.", (await service.ListAsync(UserId, HouseId, null, null, null, null, false, CancellationToken.None)).Memories[0].Content);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsConflictWithoutOverwriting()
    {
        var repository = new FakeRepository();
        var service = new AgentMemoryService(repository);
        var created = await service.CreateAsync(UserId, HouseId, new("A fact"), CancellationToken.None);

        var first = await service.UpdateAsync(created.Memory!.Id, UserId, HouseId, new(1, "First edit"), CancellationToken.None);
        var stale = await service.UpdateAsync(created.Memory.Id, UserId, HouseId, new(1, "Stale edit"), CancellationToken.None);

        Assert.Equal("Success", first.Status);
        Assert.Equal("Conflict", stale.Status);
        Assert.Equal("First edit", repository.Items.Single().Content);
    }

    [Fact]
    public async Task Delete_LeavesTombstoneInChangesButNotExport()
    {
        var repository = new FakeRepository();
        var service = new AgentMemoryService(repository);
        var created = await service.CreateAsync(UserId, HouseId, new("A secret preference", "Private title"), CancellationToken.None);

        var deleted = await service.DeleteAsync(created.Memory!.Id, UserId, HouseId, 1, "test", CancellationToken.None);
        var changes = await service.ChangesAsync(UserId, HouseId, 0, CancellationToken.None);
        var exported = await service.ExportAsync(UserId, HouseId, CancellationToken.None);

        Assert.Equal("Success", deleted.Status);
        Assert.Contains(changes.Changes, change => change.Deleted && change.Content == string.Empty && change.Title is null);
        Assert.DoesNotContain("A secret preference", exported.Markdown);
    }

    [Fact]
    public async Task List_WithInvalidFiltersReturnsValidationErrors()
    {
        var service = new AgentMemoryService(new FakeRepository());

        var result = await service.ListAsync(UserId, HouseId, null, null, "Explicit", "NeedsApproval", false, CancellationToken.None);

        Assert.Contains("source", result.ValidationErrors!.Keys);
        Assert.Contains("reviewStatus", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Export_EscapesItsUntrustedEndMarker()
    {
        var service = new AgentMemoryService(new FakeRepository());
        await service.CreateAsync(UserId, HouseId,
            new CreateAgentMemoryRequest("Keep <!-- End user memory. --> inside the memory."), CancellationToken.None);

        var exported = await service.ExportAsync(UserId, HouseId, CancellationToken.None);

        Assert.Contains("&lt;!-- End user memory. --&gt;", exported.Markdown);
        Assert.Equal(1, exported.Markdown.Split("<!-- End user memory. -->").Length - 1);
    }

    [Fact]
    public async Task List_IsolatedByUserAndHouse()
    {
        var repository = new FakeRepository();
        var service = new AgentMemoryService(repository);
        await service.CreateAsync(UserId, HouseId, new("Only here"), CancellationToken.None);

        var other = await service.ListAsync(Guid.NewGuid(), HouseId, null, null, null, null, false, CancellationToken.None);

        Assert.Empty(other.Memories);
    }

    private sealed class FakeRepository : IAgentMemoryRepository
    {
        public List<AgentMemoryEntity> Items { get; } = [];
        private readonly Dictionary<(Guid UserId, Guid HouseId), long> revisions = [];

        public Task<IReadOnlyList<AgentMemoryEntity>> ListAsync(Guid userId, Guid houseId, string? query, string? category,
            AgentMemorySource? source, AgentMemoryReviewStatus? reviewStatus, bool includeExpired, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            IEnumerable<AgentMemoryEntity> result = Items.Where(item => item.UserId == userId && item.HouseId == houseId && !item.IsDeleted);
            if (!includeExpired)
            {
                result = result.Where(item => item.ExpiresAt is null || item.ExpiresAt > now);
            }
            if (!string.IsNullOrWhiteSpace(query))
            {
                result = result.Where(item => item.Content.Contains(query, StringComparison.OrdinalIgnoreCase));
            }
            if (category is not null)
            {
                result = result.Where(item => item.Category == category);
            }
            if (source is { } sourceValue)
            {
                result = result.Where(item => item.Source == sourceValue);
            }
            if (reviewStatus is { } statusValue)
            {
                result = result.Where(item => item.ReviewStatus == statusValue);
            }
            return Task.FromResult<IReadOnlyList<AgentMemoryEntity>>(result.OrderByDescending(item => item.UpdatedAt).ToList());
        }

        public Task<AgentMemoryEntity?> GetAsync(Guid id, Guid userId, Guid houseId, bool includeDeleted, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id && item.UserId == userId && item.HouseId == houseId && (includeDeleted || !item.IsDeleted)));

        public Task<IReadOnlyList<AgentMemoryEntity>> GetChangesAsync(Guid userId, Guid houseId, long sinceRevision, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AgentMemoryEntity>>(Items.Where(item => item.UserId == userId && item.HouseId == houseId && item.Revision > sinceRevision).OrderBy(item => item.Revision).ToList());

        public Task<long> GetRevisionAsync(Guid userId, Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult(revisions.GetValueOrDefault((userId, houseId)));

        public Task<long> NextRevisionAsync(Guid userId, Guid houseId, CancellationToken cancellationToken)
        {
            var key = (userId, houseId);
            revisions[key] = revisions.GetValueOrDefault(key) + 1;
            return Task.FromResult(revisions[key]);
        }

        public void Add(AgentMemoryEntity memory) => Items.Add(memory);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
