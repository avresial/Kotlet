using Kotlet.Application.Recipes;
using Kotlet.Domain.Recipes;
using Xunit;

namespace Kotlet.Application.UnitTests.Recipes;

public sealed class RecipeImportServiceTests
{
    [Fact]
    public async Task CreateJobAsync_ValidatesAndQueuesPersistedJob()
    {
        var jobs = new FakeJobs();
        var signal = new FakeSignal();
        var service = CreateService(jobs, signal);

        var invalid = await service.CreateJobAsync(Guid.NewGuid(), Guid.NewGuid(), "not a url", default);
        var valid = await service.CreateJobAsync(Guid.NewGuid(), Guid.NewGuid(), "https://youtu.be/test", default);

        Assert.Equal(RecipeImportOperationStatus.ValidationFailed, invalid.Status);
        Assert.Equal(RecipeImportOperationStatus.Success, valid.Status);
        Assert.Equal(valid.Id, jobs.Job!.Id);
        Assert.Equal(valid.Id, signal.JobId);
        Assert.Equal(1, jobs.SaveCount);
    }

    [Fact]
    public async Task AcceptAsync_RejectsJobThatIsNotReady()
    {
        var jobs = new FakeJobs { Job = NewJob(RecipeImportJobStatus.Extracting) };
        var result = await CreateService(jobs, new FakeSignal()).AcceptAsync(
            jobs.Job.Id, jobs.Job.HouseId, jobs.Job.UserId,
            new RecipeImportDraft("Soup", 1, "Cook.", [], []), default);

        Assert.Equal(RecipeImportOperationStatus.InvalidState, result.Status);
    }

    private static RecipeImportService CreateService(FakeJobs jobs, IRecipeImportSignal signal) =>
        new(jobs, null!, null!, null!, null!, null!, null!, signal);

    private static RecipeImportJob NewJob(RecipeImportJobStatus status) => new()
    {
        Id = Guid.NewGuid(), HouseId = Guid.NewGuid(), UserId = Guid.NewGuid(), Url = "https://youtu.be/test",
        Status = status, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class FakeSignal : IRecipeImportSignal
    {
        public Guid? JobId { get; private set; }
        public void Enqueue(Guid jobId) => JobId = jobId;
        public ValueTask<Guid> WaitAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeJobs : IRecipeImportJobRepository
    {
        public RecipeImportJob? Job { get; set; }
        public int SaveCount { get; private set; }
        public Task<RecipeImportJob?> GetAsync(Guid id, Guid? houseId, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(Job is not null && Job.Id == id && (houseId is null || Job.HouseId == houseId) ? Job : null);
        public Task<IReadOnlyList<Guid>> ListActiveIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public void Add(RecipeImportJob job) => Job = job;
        public void Remove(RecipeImportJob job) => Job = null;
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
    }
}
