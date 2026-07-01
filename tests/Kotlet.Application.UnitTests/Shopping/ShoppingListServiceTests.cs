using Kotlet.Application.Shopping;
using Kotlet.Application.Translations;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.Shopping;
using Xunit;

namespace Kotlet.Application.UnitTests.Shopping;

public sealed class ShoppingListServiceTests
{
    private const string English = "en";
    private const string Polish = "pl";
    private static readonly Guid HouseId = Guid.NewGuid();
    private static readonly Ingredient Apples = Ingredient("Apples", pricePer100: 2.50m);

    // ---- GetAll ----

    [Fact]
    public async Task GetAll_ComputesTotalPriceFromQuantityAndUnitPrice()
    {
        var repo = new FakeRepository(Apples);
        repo.SeedItem(HouseId, Apples, 250m);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var item = Assert.Single(await service.GetAllAsync(HouseId, English, CancellationToken.None));

        Assert.Equal(2.50m, item.PricePer100BaseUnits);
        // 250 / 100 * 2.50 = 6.25
        Assert.Equal(6.25m, item.TotalPrice);
    }

    [Fact]
    public async Task GetAll_RoundsTotalPriceToTwoDecimals()
    {
        var ingredient = Ingredient("Oddly priced", pricePer100: 3.333m);
        var repo = new FakeRepository(ingredient);
        repo.SeedItem(HouseId, ingredient, 100m);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var item = Assert.Single(await service.GetAllAsync(HouseId, English, CancellationToken.None));

        Assert.Equal(3.33m, item.TotalPrice);
    }

    [Fact]
    public async Task GetAll_UsesTranslatedName_ForNonDefaultLanguage()
    {
        var repo = new FakeRepository(Apples);
        repo.SeedItem(HouseId, Apples, 100m);
        var translations = new FakeTranslationRepository();
        translations.Entries[TranslationKeys.Ingredient(Apples.Id, Polish)] = "Jabłka";
        var service = new ShoppingListService(repo, translations);

        var item = Assert.Single(await service.GetAllAsync(HouseId, Polish, CancellationToken.None));

        Assert.Equal("Jabłka", item.IngredientName);
    }

    // ---- Create ----

    [Fact]
    public async Task Create_AddsItem()
    {
        var repo = new FakeRepository(Apples);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var result = await service.CreateAsync(HouseId, new CreateShoppingListItemCommand(Apples.Id, 5m), English, CancellationToken.None);

        Assert.Equal(ShoppingListOperationStatus.Success, result.Status);
        Assert.NotNull(result.Item);
        Assert.False(result.Item.IsPurchased);
        Assert.Equal(1, repo.SaveCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100000000)]
    public async Task Create_WithInvalidQuantity_FailsValidation(double quantity)
    {
        var repo = new FakeRepository(Apples);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var result = await service.CreateAsync(HouseId, new CreateShoppingListItemCommand(Apples.Id, (decimal)quantity), English, CancellationToken.None);

        Assert.Equal(ShoppingListOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("quantity"));
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Create_WithUnknownIngredient_ReturnsNotFound()
    {
        var repo = new FakeRepository(Apples);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var result = await service.CreateAsync(HouseId, new CreateShoppingListItemCommand(Guid.NewGuid(), 5m), English, CancellationToken.None);

        Assert.Equal(ShoppingListOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Create_WhenIngredientAlreadyOnList_ReturnsConflict()
    {
        var repo = new FakeRepository(Apples);
        repo.SeedItem(HouseId, Apples, 5m);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var result = await service.CreateAsync(HouseId, new CreateShoppingListItemCommand(Apples.Id, 5m), English, CancellationToken.None);

        Assert.Equal(ShoppingListOperationStatus.Conflict, result.Status);
        Assert.NotNull(result.Message);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ChangesQuantityAndPurchasedFlag()
    {
        var repo = new FakeRepository(Apples);
        var item = repo.SeedItem(HouseId, Apples, 5m);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var result = await service.UpdateAsync(item.Id, HouseId, new UpdateShoppingListItemCommand(8m, true), English, CancellationToken.None);

        Assert.Equal(ShoppingListOperationStatus.Success, result.Status);
        Assert.Equal(8m, item.Quantity.Amount);
        Assert.True(item.IsPurchased);
        Assert.True(result.Item!.IsPurchased);
    }

    [Fact]
    public async Task Update_WithInvalidQuantity_FailsValidation()
    {
        var repo = new FakeRepository(Apples);
        var item = repo.SeedItem(HouseId, Apples, 5m);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var result = await service.UpdateAsync(item.Id, HouseId, new UpdateShoppingListItemCommand(0m, false), English, CancellationToken.None);

        Assert.Equal(ShoppingListOperationStatus.ValidationFailed, result.Status);
        Assert.Equal(5m, item.Quantity.Amount);
    }

    [Fact]
    public async Task Update_ForUnknownItem_ReturnsNotFound()
    {
        var repo = new FakeRepository(Apples);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), HouseId, new UpdateShoppingListItemCommand(5m, false), English, CancellationToken.None);

        Assert.Equal(ShoppingListOperationStatus.NotFound, result.Status);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_RemovesItem()
    {
        var repo = new FakeRepository(Apples);
        var item = repo.SeedItem(HouseId, Apples, 5m);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var status = await service.DeleteAsync(item.Id, HouseId, CancellationToken.None);

        Assert.Equal(ShoppingListOperationStatus.Success, status);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Delete_ForUnknownItem_ReturnsNotFound()
    {
        var repo = new FakeRepository(Apples);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var status = await service.DeleteAsync(Guid.NewGuid(), HouseId, CancellationToken.None);

        Assert.Equal(ShoppingListOperationStatus.NotFound, status);
    }

    // ---- ClearPurchased ----

    [Fact]
    public async Task ClearPurchased_RemovesOnlyPurchasedItemsAndReturnsCount()
    {
        var repo = new FakeRepository(Apples);
        var purchased = repo.SeedItem(HouseId, Apples, 5m);
        purchased.IsPurchased = true;
        repo.SeedItem(HouseId, Ingredient("Bread", 1m), 2m);
        var service = new ShoppingListService(repo, new FakeTranslationRepository());

        var removed = await service.ClearPurchasedAsync(HouseId, CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.Single(repo.Items);
        Assert.DoesNotContain(repo.Items, i => i.IsPurchased);
    }

    private static Ingredient Ingredient(string name, decimal pricePer100) => new()
    {
        Id = Guid.NewGuid(), Name = name, MeasurementUnit = "g", PricePer100BaseUnits = Price.FromAmount(pricePer100)
    };

    private sealed class FakeRepository(params Ingredient[] ingredients) : IShoppingListRepository
    {
        public List<ShoppingListItem> Items { get; } = [];
        public int SaveCount { get; private set; }

        public ShoppingListItem SeedItem(Guid houseId, Ingredient ingredient, decimal quantity)
        {
            var item = new ShoppingListItem
            {
                Id = Guid.NewGuid(), HouseId = houseId, IngredientId = ingredient.Id,
                Quantity = Quantity.FromAmount(quantity), Ingredient = ingredient
            };
            Items.Add(item);
            return item;
        }

        public Task<IReadOnlyCollection<ShoppingListItem>> GetAllAsync(Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<ShoppingListItem>>(Items.Where(i => i.HouseId == houseId).Select(Hydrate).ToArray());

        public Task<ShoppingListItem?> GetByIdAsync(Guid id, Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(i => i.Id == id && i.HouseId == houseId) is { } item ? Hydrate(item) : null);

        public Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken cancellationToken) =>
            Task.FromResult(ingredients.Any(x => x.Id == ingredientId));

        public Task<bool> ItemExistsAsync(Guid houseId, Guid ingredientId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(i => i.HouseId == houseId && i.IngredientId == ingredientId));

        public void Add(ShoppingListItem item) => Items.Add(item);
        public void Remove(ShoppingListItem item) => Items.Remove(item);

        public Task<int> RemovePurchasedAsync(Guid houseId, CancellationToken cancellationToken)
        {
            var purchased = Items.Where(i => i.HouseId == houseId && i.IsPurchased).ToList();
            foreach (var item in purchased) Items.Remove(item);
            return Task.FromResult(purchased.Count);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }

        private ShoppingListItem Hydrate(ShoppingListItem item)
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
