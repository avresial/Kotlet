using Kotlet.Application.Tools;
using Kotlet.Domain.Tools;
using Xunit;

namespace Kotlet.Application.UnitTests.Tools;

public sealed class VideoAnalysisServiceTests
{
    [Fact]
    public async Task CreateJobAsync_ValidatesSupportedHostsAndQueuesPersistedJob()
    {
        var jobs = new FakeJobs();
        var signal = new FakeSignal();
        var service = CreateService(jobs, signal);

        var invalid = await service.CreateJobAsync(
            Guid.NewGuid(), Guid.NewGuid(), "https://example.com/video", default);
        var valid = await service.CreateJobAsync(
            Guid.NewGuid(), Guid.NewGuid(), "https://youtu.be/example", default);

        Assert.Equal(VideoAnalysisOperationStatus.ValidationFailed, invalid.Status);
        Assert.Equal(VideoAnalysisOperationStatus.Success, valid.Status);
        Assert.Equal(valid.Id, jobs.Job!.Id);
        Assert.Equal(valid.Id, signal.JobId);
        Assert.Equal(1, jobs.SaveCount);
    }

    [Fact]
    public async Task ContinueAsRecipeAsync_ReturnsPreviouslyCreatedReviewJob()
    {
        var houseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reviewJobId = Guid.NewGuid();
        var jobs = new FakeJobs
        {
            Job = new VideoAnalysisJob
            {
                Id = Guid.NewGuid(),
                HouseId = houseId,
                UserId = userId,
                Url = "https://youtu.be/example",
                Status = VideoAnalysisJobStatus.Ready,
                DraftJson = "{}",
                RecipeImportJobId = reviewJobId
            }
        };
        var service = CreateService(jobs, new FakeSignal());

        var result = await service.ContinueAsRecipeAsync(
            jobs.Job.Id, houseId, userId, default);

        Assert.Equal(VideoAnalysisOperationStatus.Success, result.Status);
        Assert.Equal(reviewJobId, result.Id);
        Assert.Equal(0, jobs.SaveCount);
    }

    private static VideoAnalysisService CreateService(FakeJobs jobs, IVideoAnalysisSignal signal) =>
        new(jobs, null!, null!, null!, null!, null!, signal);

    private sealed class FakeSignal : IVideoAnalysisSignal
    {
        public Guid? JobId { get; private set; }

        public void Enqueue(Guid jobId) => JobId = jobId;

        public ValueTask<Guid> WaitAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeJobs : IVideoAnalysisJobRepository
    {
        public VideoAnalysisJob? Job { get; set; }
        public int SaveCount { get; private set; }

        public Task<VideoAnalysisJob?> GetAsync(
            Guid id,
            Guid? houseId,
            bool tracked,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Job is not null && Job.Id == id && (houseId is null || Job.HouseId == houseId)
                    ? Job
                    : null);

        public Task<IReadOnlyList<Guid>> ListActiveIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public void Add(VideoAnalysisJob job) => Job = job;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
