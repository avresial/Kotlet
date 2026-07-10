using Kotlet.Application.Pantry;
using Kotlet.Application.Recipes;
using Kotlet.Application.Translations;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Pantry;
using Kotlet.Domain.Recipes;
using Xunit;

namespace Kotlet.Application.UnitTests.Pantry;

public sealed class PantryRecipeMatchServiceTests
{
    private const string English = "en";
    private const string Polish = "pl";
    private static readonly Guid HouseId = Guid.NewGuid();

    [Fact]
    public async Task GetSuggestions_WithEmptyPantry_ReturnsEmpty()
    {
        var fixture = new Fixture();
        fixture.AddRecipe("Pancakes", fixture.Ingredient("Flour"), fixture.Ingredient("Milk"));

        var matches = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task GetSuggestions_RanksFullMatchesBeforeLargerPartialMatches()
    {
        var fixture = new Fixture();
        var flour = fixture.Ingredient("Flour");
        var milk = fixture.Ingredient("Milk");
        var eggs = fixture.Ingredient("Eggs");
        var butter = fixture.Ingredient("Butter");
        var sugar = fixture.Ingredient("Sugar");
        fixture.Stock(flour, milk, eggs, butter);
        fixture.AddRecipe("Omelette", eggs, butter);
        fixture.AddRecipe("Cake", flour, milk, eggs, butter, sugar);

        var matches = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);

        Assert.Equal(2, matches.Count);
        Assert.Equal("Omelette", matches[0].Title);
        Assert.True(matches[0].IsFullMatch);
        Assert.Equal("Cake", matches[1].Title);
        Assert.False(matches[1].IsFullMatch);
        Assert.Equal(4, matches[1].MatchedIngredientCount);
        Assert.Equal(5, matches[1].TotalIngredientCount);
        Assert.Equal("Sugar", Assert.Single(matches[1].MissingIngredients).Name);
    }

    [Fact]
    public async Task GetSuggestions_OrdersPartialMatchesByMatchedIngredientCount()
    {
        var fixture = new Fixture();
        var flour = fixture.Ingredient("Flour");
        var milk = fixture.Ingredient("Milk");
        var eggs = fixture.Ingredient("Eggs");
        fixture.Stock(flour, milk, eggs);
        fixture.AddRecipe("Bread", flour, fixture.Ingredient("Yeast"));
        fixture.AddRecipe("Pancakes", flour, milk, eggs, fixture.Ingredient("Sugar"));

        var matches = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);

        Assert.Equal(new[] { "Pancakes", "Bread" }, matches.Select(m => m.Title).ToArray());
    }

    [Fact]
    public async Task GetSuggestions_ExcludesRecipesWithoutAnyAvailableIngredient()
    {
        var fixture = new Fixture();
        fixture.Stock(fixture.Ingredient("Flour"));
        fixture.AddRecipe("Omelette", fixture.Ingredient("Eggs"), fixture.Ingredient("Butter"));

        var matches = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task GetSuggestions_IgnoresPantryItemsWithZeroQuantity()
    {
        var fixture = new Fixture();
        var flour = fixture.Ingredient("Flour");
        fixture.Stock(quantity: 0m, flour);
        fixture.AddRecipe("Bread", flour);

        var matches = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task GetSuggestions_ReturnsAtMostFiveMatches()
    {
        var fixture = new Fixture();
        var flour = fixture.Ingredient("Flour");
        fixture.Stock(flour);
        for (var i = 1; i <= 7; i++)
            fixture.AddRecipe($"Recipe {i}", flour, fixture.Ingredient($"Extra {i}"));

        var matches = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);

        Assert.Equal(5, matches.Count);
    }

    [Fact]
    public async Task GetSuggestions_WithFiveFullMatches_ReturnsOnlyThose()
    {
        var fixture = new Fixture();
        var flour = fixture.Ingredient("Flour");
        var milk = fixture.Ingredient("Milk");
        fixture.Stock(flour, milk);
        fixture.AddRecipe("Partial", flour, milk, fixture.Ingredient("Eggs"));
        for (var i = 1; i <= 6; i++)
            fixture.AddRecipe($"Full {i}", flour, milk);

        var matches = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);

        Assert.Equal(5, matches.Count);
        Assert.All(matches, match => Assert.True(match.IsFullMatch));
    }

    [Fact]
    public async Task GetSuggestions_CountsDuplicateRecipeIngredientsOnce()
    {
        var fixture = new Fixture();
        var flour = fixture.Ingredient("Flour");
        fixture.Stock(flour);
        fixture.AddRecipe("Layered dough", flour, flour);

        var match = Assert.Single(await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None));

        Assert.True(match.IsFullMatch);
        Assert.Equal(1, match.TotalIngredientCount);
        Assert.Equal(1, match.MatchedIngredientCount);
    }

    [Fact]
    public async Task GetSuggestions_UsesCachedResultUntilInvalidated()
    {
        var fixture = new Fixture();
        var flour = fixture.Ingredient("Flour");
        fixture.Stock(flour);
        fixture.AddRecipe("Bread", flour);

        var first = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);
        fixture.AddRecipe("Tortilla", flour);
        var cached = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);
        fixture.Cache.Invalidate(HouseId);
        var recomputed = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);

        Assert.Single(first);
        Assert.Single(cached);
        Assert.Equal(2, recomputed.Count);
    }

    [Fact]
    public async Task GetSuggestions_CachesPerHouse()
    {
        var fixture = new Fixture();
        var flour = fixture.Ingredient("Flour");
        fixture.Stock(flour);
        fixture.AddRecipe("Bread", flour);

        var matches = await fixture.Service.GetSuggestionsAsync(HouseId, English, CancellationToken.None);
        var otherHouse = await fixture.Service.GetSuggestionsAsync(Guid.NewGuid(), English, CancellationToken.None);

        Assert.Single(matches);
        Assert.Empty(otherHouse);
    }

    [Fact]
    public async Task GetSuggestions_TranslatesMissingIngredientNames()
    {
        var fixture = new Fixture();
        var flour = fixture.Ingredient("Flour");
        var yeast = fixture.Ingredient("Yeast");
        fixture.Stock(flour);
        fixture.AddRecipe("Bread", flour, yeast);
        fixture.Translations.Entries[TranslationKeys.Ingredient(yeast.Id, Polish)] = "Drożdże";

        var match = Assert.Single(await fixture.Service.GetSuggestionsAsync(HouseId, Polish, CancellationToken.None));

        Assert.Equal("Drożdże", Assert.Single(match.MissingIngredients).Name);
    }

    private sealed class Fixture
    {
        private readonly List<PantryItem> _pantryItems = [];
        private readonly List<Recipe> _recipes = [];

        public FakeCache Cache { get; } = new();
        public FakeTranslationRepository Translations { get; } = new();
        public PantryRecipeMatchService Service { get; }

        public Fixture() => Service = new PantryRecipeMatchService(
            new FakePantryRepository(_pantryItems), new FakeRecipeRepository(_recipes), Cache, Translations);

        public Ingredient Ingredient(string name) => new() { Id = Guid.NewGuid(), Name = name, MeasurementUnit = "g" };

        public void Stock(params Ingredient[] ingredients) => Stock(500m, ingredients);

        public void Stock(decimal quantity, params Ingredient[] ingredients)
        {
            foreach (var ingredient in ingredients)
                _pantryItems.Add(new PantryItem
                {
                    Id = Guid.NewGuid(),
                    HouseId = HouseId,
                    IngredientId = ingredient.Id,
                    Quantity = Quantity.FromAmount(quantity),
                    Ingredient = ingredient
                });
        }

        public void AddRecipe(string title, params Ingredient[] ingredients)
        {
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                HouseId = HouseId,
                OwnerUserId = Guid.NewGuid(),
                Title = title,
                Slug = title.ToLowerInvariant().Replace(' ', '-'),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            foreach (var ingredient in ingredients)
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    IngredientId = ingredient.Id,
                    NormalizedQuantity = Quantity.FromAmount(100m),
                    NormalizedUnit = "g",
                    Ingredient = ingredient
                });
            _recipes.Add(recipe);
        }
    }

    private sealed class FakeCache : IPantryRecipeMatchCache
    {
        private readonly Dictionary<Guid, IReadOnlyList<PantryRecipeMatchDto>> _entries = [];

        public bool TryGet(Guid houseId, out IReadOnlyList<PantryRecipeMatchDto>? matches)
        {
            var found = _entries.TryGetValue(houseId, out var cached);
            matches = cached;
            return found;
        }

        public void Set(Guid houseId, IReadOnlyList<PantryRecipeMatchDto> matches) => _entries[houseId] = matches;

        public void Invalidate(Guid houseId) => _entries.Remove(houseId);
    }

    private sealed class FakePantryRepository(List<PantryItem> items) : IPantryRepository
    {
        public Task<IReadOnlyCollection<PantryItem>> GetAllAsync(Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<PantryItem>>(items.Where(i => i.HouseId == houseId).ToArray());

        public Task<PantryItem?> GetByIdAsync(Guid id, Guid houseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> ItemExistsAsync(Guid houseId, Guid ingredientId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public void Add(PantryItem item) => throw new NotSupportedException();
        public void Remove(PantryItem item) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeRecipeRepository(List<Recipe> recipes) : IRecipeRepository
    {
        public Task<IReadOnlyList<Recipe>> GetAllWithIngredientsAsync(Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Recipe>>(recipes.Where(r => r.HouseId == houseId).ToList());

        public Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedAsync(
            Guid ownerUserId, int page, int pageSize, string? search, MealSlot? mealType,
            IReadOnlyCollection<Guid>? ingredientIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Recipe>> GetRecentAsync(Guid ownerUserId, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Recipe?> GetByIdAsync(Guid id, Guid ownerUserId, bool tracked, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Recipe?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Recipe>> GetAllForDuplicateCheckAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(Guid ownerUserId, string slug, Guid? excludedId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public void Add(Recipe recipe) => throw new NotSupportedException();
        public void Remove(Recipe recipe) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
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
            foreach (var key in Entries.Keys.Where(k => k.StartsWith(keyPrefix)).ToList()) Entries.Remove(key);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
