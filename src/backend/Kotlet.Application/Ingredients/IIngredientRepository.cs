using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.Ingredients;

public interface IIngredientRepository
{
    Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(string name, Guid? excludedId, CancellationToken cancellationToken);
    Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken);
    void Add(Ingredient ingredient);
    void Remove(Ingredient ingredient);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
