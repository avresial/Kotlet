using Kotlet.Domain.Houses;
using Kotlet.Domain.Pantry;

namespace Kotlet.Application.Pantry;

public interface IPantryReconciliationRepository
{
    Task<House?> GetHouseAsync(Guid houseId, CancellationToken cancellationToken);
    Task<long?> GetPantryVersionAsync(Guid houseId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PantryItem>> GetItemsAsync(Guid houseId, CancellationToken cancellationToken);
    Task<PantryReconciliationOperation?> GetOperationAsync(
        Guid houseId, string operationId, CancellationToken cancellationToken);
    Task<PantryReconciliationOperation?> GetOperationByUndoTokenAsync(
        Guid houseId, string undoToken, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, PantryUnmatchedPhrase>> GetUnmatchedPhrasesAsync(
        Guid houseId,
        IReadOnlyCollection<string> normalizedPhrases,
        string locale,
        CancellationToken cancellationToken);
    void Add(PantryItem item);
    void Remove(PantryItem item);
    void AddOperation(PantryReconciliationOperation operation);
    void AddUnmatchedPhrase(PantryUnmatchedPhrase phrase);
    /// <summary>
    /// Executes an operation in a transaction, then saves tracked changes and commits when the operation
    /// completes normally. A failure result does not roll back the transaction; callers must return failure
    /// results before mutating tracked entities, or throw to trigger rollback.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
