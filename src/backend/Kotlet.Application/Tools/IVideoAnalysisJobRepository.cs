using Kotlet.Domain.Tools;

namespace Kotlet.Application.Tools;

public interface IVideoAnalysisJobRepository
{
    Task<VideoAnalysisJob?> GetAsync(Guid id, Guid? houseId, bool tracked, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListActiveIdsAsync(CancellationToken cancellationToken);
    void Add(VideoAnalysisJob job);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
