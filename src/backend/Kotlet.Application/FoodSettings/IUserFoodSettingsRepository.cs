using Kotlet.Domain.FoodSettings;

namespace Kotlet.Application.FoodSettings;

public interface IUserFoodSettingsRepository
{
    Task<UserFoodSettings?> GetAsync(Guid userId, bool tracked, CancellationToken cancellationToken);
    Task<Guid[]> ExistingIngredientIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
    void Add(UserFoodSettings settings);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
