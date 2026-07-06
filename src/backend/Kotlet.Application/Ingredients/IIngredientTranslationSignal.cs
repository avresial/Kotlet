namespace Kotlet.Application.Ingredients;

/// <summary>
/// Wakes the background ingredient-translation worker. The producer side (<c>Notify</c>) is called
/// whenever an ingredient is added so its missing translations get filled in promptly; the consumer
/// side (<c>WaitAsync</c>) is awaited by the worker. Rapid successive notifications collapse into a
/// single pending wake-up — the worker always re-scans for <em>all</em> missing translations, so one
/// pass covers every ingredient added since the last run.
/// </summary>
public interface IIngredientTranslationSignal
{
    /// <summary>Requests a translation pass. Safe to call from any thread; never blocks.</summary>
    void Notify();

    /// <summary>Completes when a pass has been requested since the previous wait.</summary>
    ValueTask WaitAsync(CancellationToken cancellationToken);
}
