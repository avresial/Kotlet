using Kotlet.Domain.Shopping;

namespace Kotlet.Application.Shopping;

public interface IShoppingListRepository
{
    Task<IReadOnlyCollection<ShoppingListItem>> GetAllAsync(Guid houseId, CancellationToken cancellationToken);
    Task<ShoppingListItem?> GetByIdAsync(Guid id, Guid houseId, CancellationToken cancellationToken);
    Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken cancellationToken);
    Task<bool> ItemExistsAsync(Guid houseId, Guid ingredientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlannedIngredient>> GetPlannedIngredientsAsync(Guid houseId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    void Add(ShoppingListItem item);
    void Remove(ShoppingListItem item);
    Task<int> RemovePurchasedAsync(Guid houseId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record PlannedIngredient(Guid IngredientId, decimal Quantity);
