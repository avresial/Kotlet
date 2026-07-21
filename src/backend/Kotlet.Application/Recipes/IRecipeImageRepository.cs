using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public interface IRecipeImageRepository
{
    Task<bool> RecipeExistsAsync(Guid recipeId, Guid ownerUserId, CancellationToken cancellationToken);
    Task<int> CountAsync(Guid recipeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecipeImage>> ListAsync(Guid recipeId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, Guid>> GetFirstImageIdsAsync(IReadOnlyList<Guid> recipeIds, CancellationToken cancellationToken);
    Task<RecipeImage?> GetAsync(Guid recipeId, Guid imageId, CancellationToken cancellationToken);
    Task UpdateSortOrdersAsync(Guid recipeId, IReadOnlyList<Guid> imageIds, CancellationToken cancellationToken);
    void Add(RecipeImage image);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
