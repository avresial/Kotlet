using Kotlet.Application.Pantry;
using Kotlet.Application.Translations;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.Pantry;
using Xunit;

namespace Kotlet.Application.UnitTests.Pantry;

public sealed class PantryServiceTests
{
    private const string English = "en";
    private const string Polish = "pl";
    private static readonly Guid HouseId = Guid.NewGuid();
    private static readonly Ingredient Flour = Ingredient("Flour");

    // ---- GetAll ----

    [Fact]
    public async Task GetAll_ReturnsItemsForHouse()
    {
        var repo = new FakeRepository(Flour);
        repo.SeedItem(HouseId, Flour, 500m);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var items = await service.GetAllAsync(HouseId, English, CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("Flour", item.IngredientName);
        Assert.Equal(500m, item.Quantity);
        Assert.Equal("g", item.MeasurementUnit);
    }

    [Fact]
    public async Task GetAll_UsesTranslatedName_ForNonDefaultLanguage()
    {
        var repo = new FakeRepository(Flour);
        repo.SeedItem(HouseId, Flour, 500m);
        var translations = new FakeTranslationRepository();
        translations.Entries[TranslationKeys.Ingredient(Flour.Id, Polish)] = "Mąka";
        var service = new PantryService(repo, translations);

        var items = await service.GetAllAsync(HouseId, Polish, CancellationToken.None);

        Assert.Equal("Mąka", Assert.Single(items).IngredientName);
    }

    [Fact]
    public async Task GetAll_FallsBackToDefaultName_WhenTranslationMissing()
    {
        var repo = new FakeRepository(Flour);
        repo.SeedItem(HouseId, Flour, 500m);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var items = await service.GetAllAsync(HouseId, Polish, CancellationToken.None);

        Assert.Equal("Flour", Assert.Single(items).IngredientName);
    }

    // ---- Create ----

    [Fact]
    public async Task Create_AddsItem()
    {
        var repo = new FakeRepository(Flour);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var result = await service.CreateAsync(HouseId, new SavePantryItemCommand(Flour.Id, 250m), English, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.Success, result.Status);
        Assert.NotNull(result.Item);
        Assert.Equal(250m, result.Item.Quantity);
        Assert.Equal(1, repo.SaveCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100000000)]
    public async Task Create_WithInvalidQuantity_FailsValidation(double quantity)
    {
        var repo = new FakeRepository(Flour);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var result = await service.CreateAsync(HouseId, new SavePantryItemCommand(Flour.Id, (decimal)quantity), English, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("quantity"));
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Create_WithInvalidStorageLocation_FailsValidation()
    {
        var repo = new FakeRepository(Flour);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var result = await service.CreateAsync(HouseId,
            new SavePantryItemCommand(Flour.Id, 1m, StorageLocation: (StorageLocation)99), English, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.ValidationFailed, result.Status);
        Assert.Contains("storageLocation", result.ValidationErrors!);
    }

    [Fact]
    public async Task Create_WithUnknownIngredient_ReturnsNotFound()
    {
        var repo = new FakeRepository(Flour);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var result = await service.CreateAsync(HouseId, new SavePantryItemCommand(Guid.NewGuid(), 10m), English, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.NotFound, result.Status);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Create_WhenIngredientAlreadyInPantry_ReturnsConflict()
    {
        var repo = new FakeRepository(Flour);
        repo.SeedItem(HouseId, Flour, 100m);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var result = await service.CreateAsync(HouseId, new SavePantryItemCommand(Flour.Id, 250m), English, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.Conflict, result.Status);
        Assert.NotNull(result.Message);
        Assert.Equal(0, repo.SaveCount);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ChangesQuantity()
    {
        var repo = new FakeRepository(Flour);
        var item = repo.SeedItem(HouseId, Flour, 100m);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var result = await service.UpdateAsync(item.Id, HouseId, 750m, English, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.Success, result.Status);
        Assert.Equal(750m, item.Quantity.Amount);
        Assert.Equal(750m, result.Item!.Quantity);
    }

    [Fact]
    public async Task Update_WithInvalidQuantity_FailsValidation()
    {
        var repo = new FakeRepository(Flour);
        var item = repo.SeedItem(HouseId, Flour, 100m);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var result = await service.UpdateAsync(item.Id, HouseId, -5m, English, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.ValidationFailed, result.Status);
        Assert.Equal(100m, item.Quantity.Amount);
    }

    [Fact]
    public async Task Update_ForUnknownItem_ReturnsNotFound()
    {
        var repo = new FakeRepository(Flour);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), HouseId, 10m, English, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Update_ForItemInAnotherHouse_ReturnsNotFound()
    {
        var repo = new FakeRepository(Flour);
        var item = repo.SeedItem(Guid.NewGuid(), Flour, 100m);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var result = await service.UpdateAsync(item.Id, HouseId, 10m, English, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.NotFound, result.Status);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_RemovesItem()
    {
        var repo = new FakeRepository(Flour);
        var item = repo.SeedItem(HouseId, Flour, 100m);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var status = await service.DeleteAsync(item.Id, HouseId, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.Success, status);
        Assert.Empty(repo.Items);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Delete_ForUnknownItem_ReturnsNotFound()
    {
        var repo = new FakeRepository(Flour);
        var service = new PantryService(repo, new FakeTranslationRepository());

        var status = await service.DeleteAsync(Guid.NewGuid(), HouseId, CancellationToken.None);

        Assert.Equal(PantryOperationStatus.NotFound, status);
        Assert.Equal(0, repo.SaveCount);
    }

    private static Ingredient Ingredient(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MeasurementUnit = "g"
    };

    private sealed class FakeRepository(params Ingredient[] ingredients) : IPantryRepository
    {
        public List<PantryItem> Items { get; } = [];
        public int SaveCount { get; private set; }

        public PantryItem SeedItem(Guid houseId, Ingredient ingredient, decimal quantity)
        {
            var item = new PantryItem
            {
                Id = Guid.NewGuid(),
                HouseId = houseId,
                IngredientId = ingredient.Id,
                Quantity = Quantity.FromAmount(quantity),
                Ingredient = ingredient
            };
            Items.Add(item);
            return item;
        }

        public Task<IReadOnlyCollection<PantryItem>> GetAllAsync(Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<PantryItem>>(Items.Where(i => i.HouseId == houseId).Select(Hydrate).ToArray());

        public Task<PantryItem?> GetByIdAsync(Guid id, Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(i => i.Id == id && i.HouseId == houseId) is { } item ? Hydrate(item) : null);

        public Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken cancellationToken) =>
            Task.FromResult(ingredients.Any(x => x.Id == ingredientId));

        public Task<bool> ItemExistsAsync(Guid houseId, Guid ingredientId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(i => i.HouseId == houseId && i.IngredientId == ingredientId));

        public void Add(PantryItem item) => Items.Add(item);
        public void Remove(PantryItem item) => Items.Remove(item);
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }

        private PantryItem Hydrate(PantryItem item)
        {
            item.Ingredient = ingredients.Single(x => x.Id == item.IngredientId);
            return item;
        }
    }

    private sealed class FakeTranslationRepository : ITranslationRepository
    {
        public Dictionary<string, string> Entries { get; } = [];

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Entries));

        public Task SetAsync(string key, string value, CancellationToken cancellationToken) { Entries[key] = value; return Task.CompletedTask; }

        public Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken)
        {
            foreach (var key in Entries.Keys.Where(k => k.StartsWith(keyPrefix)).ToList()) Entries.Remove(key);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
