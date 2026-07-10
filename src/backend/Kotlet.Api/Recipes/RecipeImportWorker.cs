using Kotlet.Api.Persistence;
using Kotlet.Application.Recipes;

namespace Kotlet.Api.Recipes;

public sealed class RecipeImportWorker(
    IServiceScopeFactory scopeFactory,
    IRecipeImportSignal signal,
    MigrationReadySignal migrationReady,
    ILogger<RecipeImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await migrationReady.WaitAsync(stoppingToken);
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<IRecipeImportJobRepository>();
            foreach (var id in await jobs.ListActiveIdsAsync(stoppingToken)) signal.Enqueue(id);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid id;
            try { id = await signal.WaitAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RecipeImportService>().ProcessAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Recipe import {JobId} failed.", id);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<RecipeImportService>()
                        .FailAsync(id, "Recipe import failed unexpectedly.", stoppingToken);
                }
                catch (Exception failException)
                {
                    logger.LogError(failException, "Could not mark recipe import {JobId} as failed.", id);
                }
            }
        }
    }
}
