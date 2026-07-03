using Kotlet.Application.Ingredients;
using Kotlet.Application.Translations;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Xunit;

namespace Kotlet.Application.UnitTests.Ingredients;

public sealed class IngredientServiceTests
{
    private const string English = "en";
    private const string Polish = "pl";

    [Fact]
    public async Task Create_NormalizesValuesAndPersistsIngredient()
    {
        var repository = new FakeIngredientRepository();
        var translations = new FakeTranslationRepository();
        var service = new IngredientService(repository, translations);

        var result = await service.CreateAsync(
            new SaveIngredientCommand("  Chicken breast  ", " G ", false, null, 165, 12.99m),
            English,
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.Success, result.Status);
        Assert.Equal("Chicken breast", result.Ingredient!.Name);
        Assert.Equal("g", result.Ingredient.MeasurementUnit);
        Assert.Null(result.Ingredient.SvgIcon);
        Assert.NotEqual(default, result.Ingredient.CreatedAtUtc);
        Assert.Equal(1, repository.SaveCount);
        Assert.Empty(translations.Entries);
    }

    [Fact]
    public async Task Create_InNonDefaultLanguage_StoresUnknownDefaultNameAndTranslation()
    {
        var repository = new FakeIngredientRepository();
        var translations = new FakeTranslationRepository();
        var service = new IngredientService(repository, translations);

        var result = await service.CreateAsync(
            new SaveIngredientCommand("Mleko", "ml", false, null, 42, 3m),
            Polish,
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.Success, result.Status);
        // The caller (a Polish user) sees the name they typed...
        Assert.Equal("Mleko", result.Ingredient!.Name);
        // ...but the default-language name stored on the entity stays "Unknown".
        var stored = Assert.Single(repository.Ingredients);
        Assert.Equal("Unknown", stored.Name);
        Assert.Equal("Mleko", translations.Entries[TranslationKeys.Ingredient(stored.Id, Polish)]);
    }

    [Fact]
    public async Task Create_InNonDefaultLanguage_WithCanonicalNameAndTranslation_StoresBoth()
    {
        var repository = new FakeIngredientRepository();
        var translations = new FakeTranslationRepository();
        var service = new IngredientService(repository, translations);

        var result = await service.CreateAsync(
            new SaveIngredientCommand("Milk", "ml", false, null, 42, 3m, Translation: "Mleko"),
            Polish,
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.Success, result.Status);
        // The Polish user sees the translation, but the canonical English name is stored too.
        Assert.Equal("Mleko", result.Ingredient!.Name);
        Assert.Equal("Milk", result.Ingredient.DefaultName);
        Assert.Equal("Mleko", result.Ingredient.Translation);
        var stored = Assert.Single(repository.Ingredients);
        Assert.Equal("Milk", stored.Name);
        Assert.Equal("Mleko", translations.Entries[TranslationKeys.Ingredient(stored.Id, Polish)]);
    }

    [Fact]
    public async Task Update_InNonDefaultLanguage_WithCanonicalNameAndTranslation_UpdatesBoth()
    {
        var existing = Ingredient("Unknown");
        var repository = new FakeIngredientRepository(existing);
        var translations = new FakeTranslationRepository();
        var service = new IngredientService(repository, translations);

        var result = await service.UpdateAsync(
            existing.Id,
            new SaveIngredientCommand("Milk", "ml", false, null, 0, 1, Translation: "Mleko"),
            Polish,
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.Success, result.Status);
        Assert.Equal("Mleko", result.Ingredient!.Name);
        Assert.Equal("Milk", existing.Name);
        Assert.Equal("Mleko", translations.Entries[TranslationKeys.Ingredient(existing.Id, Polish)]);
    }

    [Fact]
    public async Task GetAll_ResolvesTranslatedNameForLanguageAndFallsBackToDefault()
    {
        var translated = Ingredient("Milk");
        var untranslated = Ingredient("Sugar");
        var repository = new FakeIngredientRepository(translated, untranslated);
        var translations = new FakeTranslationRepository();
        translations.Entries[TranslationKeys.Ingredient(translated.Id, Polish)] = "Mleko";
        var service = new IngredientService(repository, translations);

        var polish = await service.GetAllAsync(Polish, CancellationToken.None);

        Assert.Equal("Mleko", polish.Single(x => x.Id == translated.Id).Name);
        // No Polish translation -> fall back to the default (English) name.
        Assert.Equal("Sugar", polish.Single(x => x.Id == untranslated.Id).Name);
    }

    [Fact]
    public async Task Update_InNonDefaultLanguage_KeepsDefaultNameAndUpdatesTranslation()
    {
        var existing = Ingredient("Milk");
        var repository = new FakeIngredientRepository(existing);
        var translations = new FakeTranslationRepository();
        var service = new IngredientService(repository, translations);

        var result = await service.UpdateAsync(
            existing.Id,
            new SaveIngredientCommand("Mleko", "ml", false, null, 0, 1),
            Polish,
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.Success, result.Status);
        Assert.Equal("Mleko", result.Ingredient!.Name);
        Assert.Equal("Milk", existing.Name);
        Assert.Equal("Mleko", translations.Entries[TranslationKeys.Ingredient(existing.Id, Polish)]);
    }

    [Fact]
    public async Task Update_LeavesSvgIconUnchanged()
    {
        var existing = Ingredient("Salt");
        existing.SvgIcon = "<svg />";
        var repository = new FakeIngredientRepository(existing);
        var service = new IngredientService(repository, new FakeTranslationRepository());

        var result = await service.UpdateAsync(
            existing.Id,
            new SaveIngredientCommand("Sea salt", "g", false, null, 0, 1),
            English,
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.Success, result.Status);
        Assert.Equal("<svg />", result.Ingredient!.SvgIcon);
        Assert.Equal("<svg />", existing.SvgIcon);
    }

    [Fact]
    public async Task Create_ReturnsValidationErrorsWithoutPersisting()
    {
        var repository = new FakeIngredientRepository();
        var service = new IngredientService(repository, new FakeTranslationRepository());

        var result = await service.CreateAsync(
            new SaveIngredientCommand("", "bucket", true, null, -1, -1),
            English,
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
        var service = new IngredientService(repository, new FakeTranslationRepository());

        var result = await service.UpdateAsync(
            existing.Id,
            new SaveIngredientCommand("Pepper", "g", false, null, 0, 1),
            English,
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.Conflict, result.Status);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Create_ReturnsConflictWhenTranslatedNameAlreadyExistsInLanguage()
    {
        var existing = Ingredient("Milk");
        var repository = new FakeIngredientRepository(existing);
        var translations = new FakeTranslationRepository();
        translations.Entries[TranslationKeys.Ingredient(existing.Id, Polish)] = "Mleko";
        var service = new IngredientService(repository, translations);

        var result = await service.CreateAsync(
            new SaveIngredientCommand("Mleko", "ml", false, null, 0, 1),
            Polish,
            CancellationToken.None);

        Assert.Equal(IngredientOperationStatus.Conflict, result.Status);
    }

    private static Ingredient Ingredient(string name) => new()
    {
        Id = Guid.NewGuid(), Name = name, MeasurementUnit = "g", CaloriesPer100BaseUnits = Calories.Zero, PricePer100BaseUnits = Price.Zero
    };

    private sealed class FakeIngredientRepository(params Ingredient[] ingredients) : IIngredientRepository
    {
        private readonly List<Ingredient> _ingredients = [.. ingredients];
        public int SaveCount { get; private set; }
        public IReadOnlyList<Ingredient> Ingredients => _ingredients;

        public Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Ingredient>>(_ingredients.OrderBy(x => x.Name).ToArray());

        public Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Ingredient>>(_ingredients.Where(x => ids.Contains(x.Id)).ToDictionary(x => x.Id));

        public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(_ingredients.SingleOrDefault(x => x.Id == id));

        public Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);

        public void Add(Ingredient ingredient) => _ingredients.Add(ingredient);
        public void Remove(Ingredient ingredient) => _ingredients.Remove(ingredient);
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
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
