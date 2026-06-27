using Kotlet.Domain.Pantry;

namespace Kotlet.Application.Pantry;

public interface IPantryRepository
{
    Task<IReadOnlyCollection<PantryItem>> GetAllAsync(Guid houseId, CancellationToken cancellationToken);
    Task<PantryItem?> GetByIdAsync(Guid id, Guid houseId, CancellationToken cancellationToken);
    Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken cancellationToken);
    Task<bool> ItemExistsAsync(Guid houseId, Guid ingredientId, CancellationToken cancellationToken);
    void Add(PantryItem item);
    void Remove(PantryItem item);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
