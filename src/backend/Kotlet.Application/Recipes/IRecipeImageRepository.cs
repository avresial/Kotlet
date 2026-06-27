using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public interface IRecipeImageRepository
{
    Task<bool> RecipeExistsAsync(Guid recipeId, Guid ownerUserId, CancellationToken cancellationToken);
    Task<int> CountAsync(Guid recipeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecipeImage>> ListAsync(Guid recipeId, bool tracked, CancellationToken cancellationToken);
    Task<RecipeImage?> GetAsync(Guid recipeId, Guid imageId, bool includeContent, CancellationToken cancellationToken);
    Task<int> UpdateAltTextAsync(Guid recipeId, Guid imageId, string? altText, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken);
    Task<int> DeleteAsync(Guid recipeId, Guid imageId, CancellationToken cancellationToken);
    Task UpdateSortOrdersAsync(Guid recipeId, IReadOnlyList<Guid> imageIds, CancellationToken cancellationToken);
    void Add(RecipeImage image);
    void Remove(RecipeImage image);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
