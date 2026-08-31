using Kotlet.Application.Ingredients;
using Kotlet.Application.Translations;
using Kotlet.Domain.Ingredients;
using Xunit;

namespace Kotlet.Application.UnitTests.Ingredients;

public sealed class IngredientSearchServiceTests
{
    [Fact]
    public async Task FindClosest_SearchesCanonicalAndTranslatedNames()
    {
        var apple = new Ingredient { Id = Guid.NewGuid(), Name = "Apple", MeasurementUnit = "g" };
        var translations = new FakeTranslationRepository();
        translations.Entries[TranslationKeys.Ingredient(apple.Id, "pl")] = "Jabłko";
        var service = new IngredientSearchService(new FakeIngredientRepository(apple), translations);

        var results = await service.FindClosestAsync(["aple", "jabłko"], CancellationToken.None);

        Assert.All(results, result => Assert.Equal(apple.Id, result.IngredientId));
        Assert.Equal(("Apple", 1), (results[0].MatchedName, results[0].Distance));
        Assert.Equal(("Jabłko", 0), (results[1].MatchedName, results[1].Distance));
        Assert.Equal(("en", "g", false, 0.8m),
            (results[0].MatchedLanguage, results[0].MeasurementUnit, results[0].ExactMatch, results[0].Similarity));
        Assert.Equal(("pl", true, 1m),
            (results[1].MatchedLanguage, results[1].ExactMatch, results[1].Similarity));
    }

    [Fact]
    public async Task FindClosest_InexactName_ReturnsNearestIngredient()
    {
        var apple = new Ingredient { Id = Guid.NewGuid(), Name = "Apple", MeasurementUnit = "g" };
        var pineapple = new Ingredient { Id = Guid.NewGuid(), Name = "Pineapple", MeasurementUnit = "g" };
        var service = new IngredientSearchService(
            new FakeIngredientRepository(apple, pineapple), new FakeTranslationRepository());

        var result = Assert.Single(await service.FindClosestAsync(["aoole"], CancellationToken.None));

        Assert.Equal(apple.Id, result.IngredientId);
        Assert.Equal(("Apple", 2), (result.MatchedName, result.Distance));
        Assert.Equal(0.6m, result.Similarity);
    }

    [Fact]
    public async Task FindClosest_BlankOrEmptyCatalog_ReturnsNoMatch()
    {
        var service = new IngredientSearchService(new FakeIngredientRepository(), new FakeTranslationRepository());

        var results = await service.FindClosestAsync(["apple", " "], CancellationToken.None);

        Assert.All(results, result =>
        {
            Assert.Null(result.IngredientId);
            Assert.Null(result.Similarity);
            Assert.False(result.ExactMatch);
        });
    }

    [Fact]
    public async Task ResolveAsync_UsesSharedThresholdAndPreservesConfidence()
    {
        var apple = new Ingredient { Id = Guid.NewGuid(), Name = "Apple", MeasurementUnit = "g" };
        var search = new IngredientSearchService(
            new FakeIngredientRepository(apple),
            new FakeTranslationRepository());
        var service = new IngredientResolutionService(search);

        var results = await service.ResolveAsync(["aple", "orange"], CancellationToken.None);

        Assert.Equal(apple.Id, results[0].MatchedIngredientId);
        Assert.Equal("Apple", results[0].MatchedIngredientName);
        Assert.Equal(0.8m, results[0].MatchScore);
        Assert.False(results[0].IsProposedNew);
        Assert.Null(results[1].MatchedIngredientId);
        Assert.Equal(0.167m, results[1].MatchScore);
        Assert.True(results[1].IsProposedNew);
    }

    [Fact]
    public async Task FindClosest_LargeCatalog_ReturnsNearestMatches()
    {
        var ingredients = Enumerable.Range(0, 1000)
            .Select(index => new Ingredient
            {
                Id = Guid.NewGuid(),
                Name = $"Ingredient {index}",
                MeasurementUnit = "g"
            })
            .ToArray();
        var service = new IngredientSearchService(
            new FakeIngredientRepository(ingredients), new FakeTranslationRepository());
        var names = Enumerable.Range(0, 100)
            .Select(index => $"Ingredient {index}")
            .ToArray();

        var results = await service.FindClosestAsync(names, CancellationToken.None);

        Assert.Equal(names, results.Select(result => result.MatchedName).ToArray());
        Assert.All(results, result =>
        {
            Assert.True(result.ExactMatch);
            Assert.Equal(0, result.Distance);
        });
    }

    [Fact]
    public async Task FindClosest_TiesPreferLowestIdWhenNamesMatch()
    {
        var lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var service = new IngredientSearchService(
            new FakeIngredientRepository(
                new Ingredient { Id = higherId, Name = "Salt", MeasurementUnit = "g" },
                new Ingredient { Id = lowerId, Name = "Salt", MeasurementUnit = "g" }),
            new FakeTranslationRepository());

        var result = Assert.Single(await service.FindClosestAsync(["Salt"], CancellationToken.None));

        Assert.Equal(lowerId, result.IngredientId);
    }

    [Fact]
    public async Task FindClosest_CandidateLongerThanInput_PreservesDistance()
    {
        var pineapple = new Ingredient { Id = Guid.NewGuid(), Name = "Pineapple", MeasurementUnit = "g" };
        var service = new IngredientSearchService(
            new FakeIngredientRepository(pineapple), new FakeTranslationRepository());

        var result = Assert.Single(await service.FindClosestAsync(["Apple"], CancellationToken.None));

        Assert.Equal(pineapple.Id, result.IngredientId);
        Assert.Equal(4, result.Distance);
    }

    private sealed class FakeIngredientRepository(params Ingredient[] values) : IIngredientRepository
    {
        public Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Ingredient>>(values);
        public Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(Ingredient ingredient) => throw new NotSupportedException();
        public void Remove(Ingredient ingredient) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeTranslationRepository : ITranslationRepository
    {
        public Dictionary<string, string> Entries { get; } = [];
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(Entries);
        public Task SetAsync(string key, string value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
