using Kotlet.Application.Pantry;
using Kotlet.Domain.Houses;
using Kotlet.Domain.Pantry;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Pantry;

internal sealed class PantryReconciliationRepository(KotletDbContext dbContext) : IPantryReconciliationRepository
{
    public Task<House?> GetHouseAsync(Guid houseId, CancellationToken cancellationToken) =>
        dbContext.Houses.SingleOrDefaultAsync(house => house.Id == houseId, cancellationToken);

    public Task<long?> GetPantryVersionAsync(Guid houseId, CancellationToken cancellationToken) =>
        dbContext.Houses.AsNoTracking()
            .Where(house => house.Id == houseId)
            .Select(house => (long?)house.PantryVersion)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PantryItem>> GetItemsAsync(
        Guid houseId, CancellationToken cancellationToken) =>
        await dbContext.PantryItems
            .Include(item => item.Ingredient)
            .Where(item => item.HouseId == houseId)
            .OrderBy(item => item.Ingredient.Name)
            .ToListAsync(cancellationToken);

    public Task<PantryReconciliationOperation?> GetOperationAsync(
        Guid houseId, string operationId, CancellationToken cancellationToken) =>
        dbContext.PantryReconciliationOperations.SingleOrDefaultAsync(
            operation => operation.HouseId == houseId && operation.OperationId == operationId,
            cancellationToken);

    public Task<PantryReconciliationOperation?> GetOperationByUndoTokenAsync(
        Guid houseId, string undoToken, CancellationToken cancellationToken) =>
        dbContext.PantryReconciliationOperations.SingleOrDefaultAsync(
            operation => operation.HouseId == houseId && operation.UndoToken == undoToken,
            cancellationToken);

    public async Task<IReadOnlyDictionary<string, PantryUnmatchedPhrase>> GetUnmatchedPhrasesAsync(
        Guid houseId,
        IReadOnlyCollection<string> normalizedPhrases,
        string locale,
        CancellationToken cancellationToken)
    {
        if (normalizedPhrases.Count == 0)
        {
            return new Dictionary<string, PantryUnmatchedPhrase>(StringComparer.Ordinal);
        }

        var phrases = await dbContext.PantryUnmatchedPhrases
            .Where(phrase => phrase.HouseId == houseId
                && phrase.Locale == locale
                && normalizedPhrases.Contains(phrase.NormalizedPhrase))
            .ToListAsync(cancellationToken);
        return phrases.ToDictionary(phrase => phrase.NormalizedPhrase, StringComparer.Ordinal);
    }

    public void Add(PantryItem item)
    {
        var trackedIngredient = dbContext.Ingredients.Local
            .SingleOrDefault(ingredient => ingredient.Id == item.IngredientId);
        if (trackedIngredient is not null)
        {
            item.Ingredient = trackedIngredient;
        }
        else if (item.Ingredient is not null)
        {
            dbContext.Entry(item.Ingredient).State = EntityState.Unchanged;
        }

        dbContext.PantryItems.Add(item);
    }

    public void Remove(PantryItem item) => dbContext.PantryItems.Remove(item);

    public void AddOperation(PantryReconciliationOperation operation) =>
        dbContext.PantryReconciliationOperations.Add(operation);

    public void AddUnmatchedPhrase(PantryUnmatchedPhrase phrase) =>
        dbContext.PantryUnmatchedPhrases.Add(phrase);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new PantryConcurrencyException(exception);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
