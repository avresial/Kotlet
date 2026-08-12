using Kotlet.Application.Tools;
using Kotlet.Domain.Tools;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Tools;

internal sealed class VideoAnalysisJobRepository(KotletDbContext dbContext) : IVideoAnalysisJobRepository
{
    public Task<VideoAnalysisJob?> GetAsync(
        Guid id,
        Guid? houseId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<VideoAnalysisJob> query = tracked
            ? dbContext.VideoAnalysisJobs
            : dbContext.VideoAnalysisJobs.AsNoTracking();
        return query.SingleOrDefaultAsync(
            job => job.Id == id && (houseId == null || job.HouseId == houseId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListActiveIdsAsync(CancellationToken cancellationToken) =>
        await dbContext.VideoAnalysisJobs.AsNoTracking()
            .Where(job => job.Status != VideoAnalysisJobStatus.Ready && job.Status != VideoAnalysisJobStatus.Failed)
            .Select(job => job.Id)
            .ToListAsync(cancellationToken);

    public void Add(VideoAnalysisJob job) => dbContext.VideoAnalysisJobs.Add(job);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
