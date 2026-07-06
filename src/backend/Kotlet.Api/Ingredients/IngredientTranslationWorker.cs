using Kotlet.Api.Persistence;
using Kotlet.Application.Ingredients;

namespace Kotlet.Api.Ingredients;

/// <summary>
/// Background worker that keeps ingredient-name translations complete. It runs one pass at startup
/// (once the database schema and seeded ingredients are ready) and then again whenever an ingredient
/// is added, driven by <see cref="IIngredientTranslationSignal"/>. Each pass uses the application-level
/// AI credentials, so it works without any user having configured a provider — and quietly does
/// nothing when those credentials are absent.
/// </summary>
public sealed class IngredientTranslationWorker(
    IServiceScopeFactory scopeFactory,
    IIngredientTranslationSignal signal,
    MigrationReadySignal migrationReady,
    ILogger<IngredientTranslationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait until migrations have run and reference ingredients are seeded before the first pass.
        await migrationReady.WaitAsync(stoppingToken);

        // Startup backfill covers everything already in the database.
        await RunPassAsync(stoppingToken);

        // Then react to each ingredient added during the process lifetime.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await signal.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunPassAsync(stoppingToken);
        }
    }

    private async Task RunPassAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IngredientTranslationService>();
            var result = await service.BackfillMissingTranslationsAsync(stoppingToken);
            var autofilled = await scope.ServiceProvider.GetRequiredService<IngredientDetailsAutofillService>()
                .BackfillAsync(stoppingToken);

            if (!result.ProviderConfigured)
                logger.LogDebug("Ingredient translation skipped: no application AI credentials configured.");
            else if (result.Written > 0 || result.Failed > 0)
                logger.LogInformation(
                    "Ingredient translation pass complete: {Written} written, {Failed} failed.",
                    result.Written, result.Failed);
            if (autofilled > 0)
                logger.LogInformation("Ingredient details autofill pass complete: {Written} written.", autofilled);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down — nothing to log.
        }
        catch (Exception exception)
        {
            // A failed pass must not tear down the worker; the next signal (or restart) retries.
            logger.LogError(exception, "Ingredient translation pass failed.");
        }
    }
}
