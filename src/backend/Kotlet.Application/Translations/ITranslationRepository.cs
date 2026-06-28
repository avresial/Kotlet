namespace Kotlet.Application.Translations;

public interface ITranslationRepository
{
    /// <summary>Loads the entire dictionary as a key/value map.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Inserts or updates a single key. Changes are not persisted until <see cref="SaveChangesAsync"/>.</summary>
    Task SetAsync(string key, string value, CancellationToken cancellationToken);

    /// <summary>Removes every key that starts with the given prefix. Changes are not persisted until <see cref="SaveChangesAsync"/>.</summary>
    Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
