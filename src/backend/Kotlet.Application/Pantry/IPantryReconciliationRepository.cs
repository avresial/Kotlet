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
    Task<PantryUnmatchedPhrase?> GetUnmatchedPhraseAsync(
        Guid houseId, string normalizedPhrase, string locale, CancellationToken cancellationToken);
    void Add(PantryItem item);
    void Remove(PantryItem item);
    void AddOperation(PantryReconciliationOperation operation);
    void AddUnmatchedPhrase(PantryUnmatchedPhrase phrase);
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
