using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public interface IRecipeImportJobRepository
{
    Task<RecipeImportJob?> GetAsync(Guid id, Guid? houseId, bool tracked, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListActiveIdsAsync(CancellationToken cancellationToken);
    void Add(RecipeImportJob job);
    void Remove(RecipeImportJob job);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
