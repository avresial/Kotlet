using Kotlet.Api.Persistence;
using Kotlet.Application.Tools;

namespace Kotlet.Api.Tools;

public sealed class VideoAnalysisWorker(
    IServiceScopeFactory scopeFactory,
    IVideoAnalysisSignal signal,
    MigrationReadySignal migrationReady,
    ILogger<VideoAnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await migrationReady.WaitAsync(stoppingToken);
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<IVideoAnalysisJobRepository>();
            foreach (var id in await jobs.ListActiveIdsAsync(stoppingToken))
            {
                signal.Enqueue(id);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid id;
            try
            {
                id = await signal.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<VideoAnalysisService>()
                    .ProcessAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Video analysis {JobId} failed.", id);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<VideoAnalysisService>()
                        .FailAsync(id, "Video analysis failed unexpectedly.", stoppingToken);
                }
                catch (Exception failException)
                {
                    logger.LogError(failException, "Could not mark video analysis {JobId} as failed.", id);
                }
            }
        }
    }
}
