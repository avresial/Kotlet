using Kotlet.Application.Ingredients;
using Kotlet.Domain.Ingredients;
using Xunit;

namespace Kotlet.Application.UnitTests.Ingredients;

public sealed class IngredientServiceTests
{
    [Fact]
    public async Task Create_NormalizesValuesAndPersistsIngredient()
    {
        var repository = new FakeIngredientRepository();
        var service = new IngredientService(repository);

        var result = await service.CreateAsync(
            new SaveIngredientCommand("  Chicken breast  ", " G ", false, null, 165, 12.99m),
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.Success, result.Status);
        Assert.Equal("Chicken breast", result.Ingredient!.Name);
        Assert.Equal("g", result.Ingredient.MeasurementUnit);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Create_ReturnsValidationErrorsWithoutPersisting()
    {
        var repository = new FakeIngredientRepository();
        var service = new IngredientService(repository);

        var result = await service.CreateAsync(
            new SaveIngredientCommand("", "bucket", true, null, -1, -1),
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.ValidationFailed, result.Status);
        Assert.Equal(5, result.ValidationErrors!.Count);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Update_ReturnsConflictWhenNameBelongsToAnotherIngredient()
    {
        var existing = Ingredient("Salt");
        var repository = new FakeIngredientRepository(existing, Ingredient("Pepper"));
        var service = new IngredientService(repository);

        var result = await service.UpdateAsync(
            existing.Id,
            new SaveIngredientCommand("Pepper", "g", false, null, 0, 1),
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.Conflict, result.Status);
        Assert.Equal(0, repository.SaveCount);
    }

    private static Ingredient Ingredient(string name) => new()
    {
        Id = Guid.NewGuid(), Name = name, MeasurementUnit = "g", CaloriesPer100BaseUnits = 0, PricePer100BaseUnits = 0
    };

    private sealed class FakeIngredientRepository(params Ingredient[] ingredients) : IIngredientRepository
    {
        private readonly List<Ingredient> _ingredients = [.. ingredients];
        public int SaveCount { get; private set; }

        public Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Ingredient>>(_ingredients.OrderBy(x => x.Name).ToArray());

        public Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Ingredient>>(_ingredients.Where(x => ids.Contains(x.Id)).ToDictionary(x => x.Id));

        public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(_ingredients.SingleOrDefault(x => x.Id == id));

        public Task<bool> NameExistsAsync(string name, Guid? excludedId, CancellationToken cancellationToken) =>
            Task.FromResult(_ingredients.Any(x =>
                x.Id != excludedId && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);

        public void Add(Ingredient ingredient) => _ingredients.Add(ingredient);
        public void Remove(Ingredient ingredient) => _ingredients.Remove(ingredient);
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
    }
}
