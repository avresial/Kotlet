using Kotlet.Application.Ingredients;
using Kotlet.Application.Translations;
using Kotlet.Domain.Ingredients;
using Xunit;

namespace Kotlet.Application.UnitTests.Ingredients;

public sealed class IngredientBatchResolutionServiceTests
{
    private const string English = "en";

    [Fact]
    public async Task Resolve_ExactCaseInsensitiveMatch_ReturnsExisting()
    {
        var chickpeas = Ingredient("Chickpeas");
        var service = CreateService(chickpeas);

        var result = await service.ResolveAsync(
            [new("chickpeas")], createMissing: false, English, CancellationToken.None);

        var resolved = Assert.Single(result.Resolved);
        Assert.Equal(chickpeas.Id, resolved.IngredientId);
        Assert.Equal("Chickpeas", resolved.MatchedName);
        Assert.Equal(IngredientResolutionStatus.Existing, resolved.Status);
        Assert.Empty(result.Ambiguous);
        Assert.Empty(result.Missing);
    }

    [Fact]
    public async Task Resolve_SingularInput_MatchesPluralCatalogName()
    {
        var chickpeas = Ingredient("Chickpeas");
        var service = CreateService(chickpeas);

        var result = await service.ResolveAsync(
            [new("chickpea")], createMissing: false, English, CancellationToken.None);

        var resolved = Assert.Single(result.Resolved);
        Assert.Equal(chickpeas.Id, resolved.IngredientId);
        Assert.Equal(IngredientResolutionStatus.Existing, resolved.Status);
    }

    [Fact]
    public async Task Resolve_ExactMatchWins_OverPartialMatches()
    {
        var tomato = Ingredient("Tomato");
        var passata = Ingredient("Tomato passata");
        var service = CreateService(tomato, passata);

        var result = await service.ResolveAsync(
            [new("tomato")], createMissing: false, English, CancellationToken.None);

        var resolved = Assert.Single(result.Resolved);
        Assert.Equal(tomato.Id, resolved.IngredientId);
        Assert.Empty(result.Ambiguous);
    }

    [Fact]
    public async Task Resolve_MultiplePartialMatches_ReturnsAmbiguousWithoutGuessing()
    {
        var passata = Ingredient("Tomato passata");
        var paste = Ingredient("Tomato paste");
        var service = CreateService(passata, paste);

        var result = await service.ResolveAsync(
            [new("tomato")], createMissing: true, English, CancellationToken.None);

        Assert.Empty(result.Resolved);
        var ambiguous = Assert.Single(result.Ambiguous);
        Assert.Equal("tomato", ambiguous.InputName);
        Assert.Equal(2, ambiguous.Matches.Count);
        Assert.Contains(ambiguous.Matches, m => m.IngredientId == passata.Id);
        Assert.Contains(ambiguous.Matches, m => m.IngredientId == paste.Id);
        // Ambiguous names are never auto-created, even with createMissing=true.
        Assert.Empty(result.Missing);
    }

    [Fact]
    public async Task Resolve_NoMatchWithoutCreateMissing_ReturnsMissing()
    {
        var service = CreateService();

        var result = await service.ResolveAsync(
            [new("dragon fruit")], createMissing: false, English, CancellationToken.None);

        var missing = Assert.Single(result.Missing);
        Assert.Equal("dragon fruit", missing.InputName);
        Assert.Contains("createMissing=false", missing.Reason);
    }

    [Fact]
    public async Task Resolve_CreateMissing_CreatesWithHintsAndDefaults()
    {
        var repository = new FakeIngredientRepository();
        var service = CreateService(repository);

        var result = await service.ResolveAsync(
            [new("Chickpeas", ExpectedUnit: "g", CategoryHint: "Legume", CaloriesPer100BaseUnits: 164)],
            createMissing: true, English, CancellationToken.None);

        var resolved = Assert.Single(result.Resolved);
        Assert.Equal(IngredientResolutionStatus.Created, resolved.Status);
        Assert.Equal("Chickpeas", resolved.MatchedName);
        var stored = Assert.Single(repository.Ingredients);
        Assert.Equal("g", stored.MeasurementUnit);
        Assert.Equal(FoodCategory.Legume, stored.Category);
        Assert.Equal(164, stored.CaloriesPer100BaseUnits.Kilocalories);
        Assert.Equal(0, stored.PricePer100BaseUnits.Amount);
    }

    [Fact]
    public async Task Resolve_CreateMissing_WithUnknownHints_FallsBackToDefaults()
    {
        var repository = new FakeIngredientRepository();
        var service = CreateService(repository);

        var result = await service.ResolveAsync(
            [new("Mystery spice", ExpectedUnit: "handful", CategoryHint: "NotACategory")],
            createMissing: true, English, CancellationToken.None);

        Assert.Single(result.Resolved);
        var stored = Assert.Single(repository.Ingredients);
        Assert.Equal("g", stored.MeasurementUnit);
        Assert.Equal(FoodCategory.Unknown, stored.Category);
        Assert.Equal(0, stored.CaloriesPer100BaseUnits.Kilocalories);
    }

    [Fact]
    public async Task Resolve_CreateMissing_PcsUnit_CreatesCountableIngredient()
    {
        var repository = new FakeIngredientRepository();
        var service = CreateService(repository);

        await service.ResolveAsync(
            [new("Free-range egg", ExpectedUnit: "pcs", MeasurementUnitsPerPiece: 60)],
            createMissing: true, English, CancellationToken.None);

        var stored = Assert.Single(repository.Ingredients);
        Assert.True(stored.IsCountable);
        Assert.Equal(60, stored.MeasurementUnitsPerPiece);
        Assert.Equal("g", stored.MeasurementUnit);
    }

    [Fact]
    public async Task Resolve_DuplicateNameWithinBatch_ReusesIngredientCreatedEarlierInBatch()
    {
        var repository = new FakeIngredientRepository();
        var service = CreateService(repository);

        var result = await service.ResolveAsync(
            [new("Chickpeas"), new("chickpeas")], createMissing: true, English, CancellationToken.None);

        Assert.Equal(2, result.Resolved.Count);
        Assert.Equal(IngredientResolutionStatus.Created, result.Resolved[0].Status);
        Assert.Equal(IngredientResolutionStatus.Existing, result.Resolved[1].Status);
        var stored = Assert.Single(repository.Ingredients);
        Assert.Equal(result.Resolved[0].IngredientId, stored.Id);
        Assert.Equal(result.Resolved[1].IngredientId, stored.Id);
    }

    [Fact]
    public async Task Resolve_MixedBatch_ClassifiesEveryCandidate()
    {
        var chickpeas = Ingredient("Chickpeas");
        var passata = Ingredient("Tomato passata");
        var paste = Ingredient("Tomato paste");
        var service = CreateService(chickpeas, passata, paste);

        var result = await service.ResolveAsync(
            [new("chickpeas"), new("tomato"), new("unknown ingredient")],
            createMissing: false, English, CancellationToken.None);

        Assert.Equal(chickpeas.Id, Assert.Single(result.Resolved).IngredientId);
        Assert.Equal("tomato", Assert.Single(result.Ambiguous).InputName);
        Assert.Equal("unknown ingredient", Assert.Single(result.Missing).InputName);
    }

    [Fact]
    public async Task Resolve_BlankName_ReturnsMissingWithReason()
    {
        var service = CreateService();

        var result = await service.ResolveAsync(
            [new("   ")], createMissing: true, English, CancellationToken.None);

        var missing = Assert.Single(result.Missing);
        Assert.Contains("required", missing.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static IngredientBatchResolutionService CreateService(params Ingredient[] ingredients) =>
        CreateService(new FakeIngredientRepository(ingredients));

    private static IngredientBatchResolutionService CreateService(FakeIngredientRepository repository) =>
        new(new IngredientService(repository, new FakeTranslationRepository(), new IngredientTranslationSignal()));

    private static Ingredient Ingredient(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MeasurementUnit = "g"
    };

    private sealed class FakeIngredientRepository(params Ingredient[] ingredients) : IIngredientRepository
    {
        private readonly List<Ingredient> _ingredients = [.. ingredients];
        public IReadOnlyList<Ingredient> Ingredients => _ingredients;

        public Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Ingredient>>(_ingredients.ToArray());

        public Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Ingredient>>(_ingredients.Where(x => ids.Contains(x.Id)).ToDictionary(x => x.Id));

        public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(_ingredients.SingleOrDefault(x => x.Id == id));

        public Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);

        public void Add(Ingredient ingredient) => _ingredients.Add(ingredient);
        public void Remove(Ingredient ingredient) => _ingredients.Remove(ingredient);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTranslationRepository : ITranslationRepository
    {
        public Dictionary<string, string> Entries { get; } = [];

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Entries));

        public Task SetAsync(string key, string value, CancellationToken cancellationToken)
        {
            Entries[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken)
        {
            foreach (var key in Entries.Keys.Where(k => k.StartsWith(keyPrefix)).ToList())
                Entries.Remove(key);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
