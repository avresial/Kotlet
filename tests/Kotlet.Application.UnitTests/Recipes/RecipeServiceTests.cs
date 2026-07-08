using Kotlet.Application.Recipes;
using Kotlet.Application.Ingredients;
using Kotlet.Application.Measurements;
using Kotlet.Application.Translations;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Recipes;
using Xunit;

namespace Kotlet.Application.UnitTests.Recipes;

public sealed class RecipeServiceTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OtherOwnerId = Guid.NewGuid();
    private static readonly Ingredient Tomatoes = Ingredient("Tomatoes");
    private static readonly Ingredient Garlic = Ingredient("Garlic");
    private static readonly Ingredient Pasta = Ingredient("Pasta");

    private static CreateRecipeRequest ValidCreateRequest(string title = "Tomato Soup") =>
        new(title, "Simple **tomato soup**.", [
            new(Tomatoes.Id, 800, "g", "canned"),
            new(Garlic.Id, 2, "g", null)
        ]);

    // ---- Slug generation ----

    [Theory]
    [InlineData("Tomato Soup", "tomato-soup")]
    [InlineData("  Chicken & Rice  ", "chicken-rice")]
    [InlineData("Crème brûlée", "crme-brle")]
    [InlineData("Hello   World", "hello-world")]
    public void GenerateSlug_ProducesExpectedSlug(string title, string expectedSlug)
    {
        Assert.Equal(expectedSlug, RecipeService.GenerateSlug(title));
    }

    // ---- Create ----

    [Fact]
    public async Task Create_WithValidData_PersistsAndReturnsRecipe()
    {
        var repo = new FakeRecipeRepository();
        var service = CreateService(repo);

        var result = await service.CreateAsync(OwnerId, OwnerId, ValidCreateRequest(), CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.Success, result.Status);
        Assert.NotNull(result.Recipe);
        Assert.Equal("Tomato Soup", result.Recipe.Title);
        Assert.Equal("tomato-soup", result.Recipe.Slug);
        Assert.Equal(2, result.Recipe.Ingredients.Count);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Create_SetsOwnerUserId()
    {
        var repo = new FakeRecipeRepository();
        var service = CreateService(repo);

        await service.CreateAsync(OwnerId, OwnerId, ValidCreateRequest(), CancellationToken.None);

        Assert.Equal(OwnerId, repo.Recipes.Single().OwnerUserId);
    }

    [Fact]
    public async Task Create_ExposesCreatedByUserId()
    {
        var repo = new FakeRecipeRepository();
        var service = CreateService(repo);

        var result = await service.CreateAsync(OwnerId, OwnerId, ValidCreateRequest(), CancellationToken.None);

        Assert.NotNull(result.Recipe);
        Assert.Equal(OwnerId, result.Recipe.CreatedByUserId);
    }

    [Fact]
    public async Task Create_WithEmptyTitle_ReturnsValidationFailed()
    {
        var repo = new FakeRecipeRepository();
        var service = CreateService(repo);

        var result = await service.CreateAsync(OwnerId, OwnerId,
            new CreateRecipeRequest("", null, []), CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Create_WithTooLongTitle_ReturnsValidationFailed()
    {
        var repo = new FakeRecipeRepository();
        var service = CreateService(repo);

        var result = await service.CreateAsync(OwnerId, OwnerId,
            new CreateRecipeRequest(new string('A', 161), null, []), CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public async Task Create_WithInvalidIngredient_ReturnsValidationFailed()
    {
        var repo = new FakeRecipeRepository();
        var service = CreateService(repo);

        var result = await service.CreateAsync(OwnerId, OwnerId,
            new CreateRecipeRequest("Soup", null, [new(Guid.Empty, 0, "", null)]), CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("ingredients"));
    }

    [Fact]
    public async Task Create_WithNegativeQuantity_ReturnsValidationFailed()
    {
        var repo = new FakeRecipeRepository();
        var service = CreateService(repo);

        var result = await service.CreateAsync(OwnerId, OwnerId,
            new CreateRecipeRequest("Soup", null, [new(Tomatoes.Id, -1, "g", null)]), CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("ingredients"));
    }

    [Fact]
    public async Task Create_WithInvalidMealType_ReturnsValidationFailed()
    {
        var service = CreateService(new FakeRecipeRepository());

        var result = await service.CreateAsync(OwnerId, OwnerId,
            ValidCreateRequest() with { MealType = "brunch" }, CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.ValidationFailed, result.Status);
        Assert.Contains("mealType", result.ValidationErrors!);
    }

    [Fact]
    public async Task Create_SlugCollision_ResolvesByAppendingNumber()
    {
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(MakeRecipe("Tomato Soup", "tomato-soup", OwnerId));
        var service = CreateService(repo);

        var result = await service.CreateAsync(OwnerId, OwnerId, ValidCreateRequest(), CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.Success, result.Status);
        Assert.Equal("tomato-soup-2", result.Recipe!.Slug);
    }

    [Fact]
    public async Task Create_SameSlug_DifferentOwners_BothSucceed()
    {
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(MakeRecipe("Tomato Soup", "tomato-soup", OtherOwnerId));
        var service = CreateService(repo);

        var result = await service.CreateAsync(OwnerId, OwnerId, ValidCreateRequest(), CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.Success, result.Status);
        Assert.Equal("tomato-soup", result.Recipe!.Slug);
    }

    // ---- List ----

    [Fact]
    public async Task List_ReturnsOnlyOwnerRecipes()
    {
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(MakeRecipe("My Recipe", "my-recipe", OwnerId));
        repo.Recipes.Add(MakeRecipe("Other Recipe", "other-recipe", OtherOwnerId));
        var service = CreateService(repo);

        var result = await service.ListAsync(OwnerId, 1, 20, null, null, null, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("My Recipe", result.Items[0].Title);
    }

    [Fact]
    public async Task List_FiltersByMealType()
    {
        var repo = new FakeRecipeRepository();
        var breakfast = MakeRecipe("Breakfast", "breakfast", OwnerId);
        breakfast.MealType = MealSlot.Breakfast;
        var dinner = MakeRecipe("Dinner", "dinner", OwnerId);
        dinner.MealType = MealSlot.Dinner;
        repo.Recipes.AddRange([breakfast, dinner]);

        var result = await CreateService(repo).ListAsync(OwnerId, 1, 20, null, "breakfast", null, CancellationToken.None);

        Assert.Equal("Breakfast", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task ListRecent_ReturnsNewestCreatedOwnerRecipes()
    {
        var repo = new FakeRecipeRepository();
        var older = MakeRecipe("Older", "older", OwnerId);
        older.CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2);
        var newer = MakeRecipe("Newer", "newer", OwnerId);
        repo.Recipes.AddRange([older, newer, MakeRecipe("Other", "other", OtherOwnerId)]);

        var result = await CreateService(repo).ListRecentAsync(OwnerId, 1, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Newer", result[0].Title);
    }

    // ---- GetById ----

    [Fact]
    public async Task GetById_ReturnsNull_WhenBelongsToOtherUser()
    {
        var recipe = MakeRecipe("My Recipe", "my-recipe", OtherOwnerId);
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(recipe);
        var service = CreateService(repo);

        var result = await service.GetByIdAsync(recipe.Id, OwnerId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsRecipe_WhenOwnedByCurrentUser()
    {
        var recipe = MakeRecipe("My Recipe", "my-recipe", OwnerId);
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(recipe);
        var service = CreateService(repo);

        var result = await service.GetByIdAsync(recipe.Id, OwnerId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("My Recipe", result.Title);
    }

    [Fact]
    public async Task GetById_TranslatesIngredientNames()
    {
        var recipe = MakeRecipe("My Recipe", "my-recipe", OwnerId);
        recipe.Ingredients.Add(new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            IngredientId = Tomatoes.Id,
            Ingredient = Tomatoes,
            NormalizedQuantity = Quantity.FromAmount(500),
            NormalizedUnit = "g",
            SortOrder = 0
        });

        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(recipe);

        var translations = new FakeTranslationRepository();
        await translations.SetAsync(TranslationKeys.Ingredient(Tomatoes.Id, "pl"), "Pomidory", CancellationToken.None);

        var service = CreateService(repo, translations);

        var result = await service.GetByIdAsync(recipe.Id, OwnerId, CancellationToken.None, "pl");

        Assert.NotNull(result);
        var ing = Assert.Single(result.Ingredients);
        Assert.Equal("Pomidory", ing.Name);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_UpdatesTitleDescriptionAndIngredients()
    {
        var recipe = MakeRecipe("Old Title", "old-title", OwnerId);
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(recipe);
        var service = CreateService(repo);

        var request = new UpdateRecipeRequest("New Title", "Updated desc", [new(Pasta.Id, 200, "g", null)]);
        var result = await service.UpdateAsync(recipe.Id, OwnerId, request, CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.Success, result.Status);
        Assert.Equal("New Title", result.Recipe!.Title);
        Assert.Equal("new-title", result.Recipe.Slug);
        Assert.Single(result.Recipe.Ingredients);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenBelongsToOtherUser()
    {
        var recipe = MakeRecipe("Recipe", "recipe", OtherOwnerId);
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(recipe);
        var service = CreateService(repo);

        var request = new UpdateRecipeRequest("New Title", null, []);
        var result = await service.UpdateAsync(recipe.Id, OwnerId, request, CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.NotFound, result.Status);
        Assert.Equal(0, repo.SaveCount);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_RemovesRecipe()
    {
        var recipe = MakeRecipe("Recipe", "recipe", OwnerId);
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(recipe);
        var service = CreateService(repo);

        var status = await service.DeleteAsync(recipe.Id, OwnerId, CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.Success, status);
        Assert.Empty(repo.Recipes);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenBelongsToOtherUser()
    {
        var recipe = MakeRecipe("Recipe", "recipe", OtherOwnerId);
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(recipe);
        var service = CreateService(repo);

        var status = await service.DeleteAsync(recipe.Id, OwnerId, CancellationToken.None);

        Assert.Equal(RecipeOperationStatus.NotFound, status);
        Assert.Single(repo.Recipes);
        Assert.Equal(0, repo.SaveCount);
    }

    // ---- CheckExists (duplicate detection) ----

    [Fact]
    public async Task CheckExists_MatchesBySourceUrlCitedInDescription()
    {
        var repo = new FakeRecipeRepository();
        var recipe = MakeRecipe("Goulash", "goulash", OwnerId);
        recipe.DescriptionMarkdown = "Rich stew.\n\n1. Brown the meat.\n\nSource: https://example.com/goulash";
        repo.Recipes.Add(recipe);
        var service = CreateService(repo);

        var result = await service.CheckExistsAsync(OwnerId, null, "https://example.com/goulash/", CancellationToken.None);

        Assert.True(result.Exists);
        var match = Assert.Single(result.Matches);
        Assert.Equal(recipe.Id, match.RecipeId);
        Assert.Equal(RecipeMatchType.SourceUrl, match.MatchType);
        Assert.Equal("https://example.com/goulash", match.SourceUrl);
    }

    [Fact]
    public async Task CheckExists_DoesNotMatchLongerUrlWithSamePrefix()
    {
        var repo = new FakeRecipeRepository();
        var recipe = MakeRecipe("Goulash deluxe", "goulash-deluxe", OwnerId);
        recipe.DescriptionMarkdown = "Source: https://example.com/goulash-deluxe";
        repo.Recipes.Add(recipe);
        var service = CreateService(repo);

        var result = await service.CheckExistsAsync(OwnerId, null, "https://example.com/goulash", CancellationToken.None);

        Assert.False(result.Exists);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task CheckExists_MatchesExactTitle_IgnoringCaseAndPunctuation()
    {
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(MakeRecipe("Chickpea Balls with Tomato Sauce", "chickpea-balls", OwnerId));
        var service = CreateService(repo);

        var result = await service.CheckExistsAsync(
            OwnerId, "chickpea balls, with tomato sauce!", null, CancellationToken.None);

        Assert.True(result.Exists);
        Assert.Equal(RecipeMatchType.ExactTitle, Assert.Single(result.Matches).MatchType);
    }

    [Fact]
    public async Task CheckExists_ReturnsSimilarTitleMatch_ButNotUnrelatedTitles()
    {
        var repo = new FakeRecipeRepository();
        var similar = MakeRecipe("Chickpea balls with tomato sauce", "chickpea-balls", OwnerId);
        var unrelated = MakeRecipe("Chocolate cake", "chocolate-cake", OwnerId);
        repo.Recipes.Add(similar);
        repo.Recipes.Add(unrelated);
        var service = CreateService(repo);

        var result = await service.CheckExistsAsync(
            OwnerId, "Chickpea balls in tomato sauce", null, CancellationToken.None);

        Assert.True(result.Exists);
        var match = Assert.Single(result.Matches);
        Assert.Equal(similar.Id, match.RecipeId);
        Assert.Equal(RecipeMatchType.SimilarTitle, match.MatchType);
    }

    [Fact]
    public async Task CheckExists_OrdersSourceUrlMatchBeforeTitleMatches()
    {
        var repo = new FakeRecipeRepository();
        var byTitle = MakeRecipe("Goulash", "goulash", OwnerId);
        var byUrl = MakeRecipe("Hungarian stew", "hungarian-stew", OwnerId);
        byUrl.DescriptionMarkdown = "Source: https://example.com/goulash";
        repo.Recipes.Add(byTitle);
        repo.Recipes.Add(byUrl);
        var service = CreateService(repo);

        var result = await service.CheckExistsAsync(
            OwnerId, "Goulash", "https://example.com/goulash", CancellationToken.None);

        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(byUrl.Id, result.Matches[0].RecipeId);
        Assert.Equal(RecipeMatchType.SourceUrl, result.Matches[0].MatchType);
        Assert.Equal(byTitle.Id, result.Matches[1].RecipeId);
        Assert.Equal(RecipeMatchType.ExactTitle, result.Matches[1].MatchType);
    }

    [Fact]
    public async Task CheckExists_WithNoMatch_ReturnsEmptyResult()
    {
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(MakeRecipe("Chocolate cake", "chocolate-cake", OwnerId));
        var service = CreateService(repo);

        var result = await service.CheckExistsAsync(
            OwnerId, "Lentil curry", "https://example.com/lentil-curry", CancellationToken.None);

        Assert.False(result.Exists);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task CheckExists_IgnoresRecipesOfOtherHouseholds()
    {
        var repo = new FakeRecipeRepository();
        repo.Recipes.Add(MakeRecipe("Goulash", "goulash", OtherOwnerId));
        var service = CreateService(repo);

        var result = await service.CheckExistsAsync(OwnerId, "Goulash", null, CancellationToken.None);

        Assert.False(result.Exists);
    }

    private static Recipe MakeRecipe(string title, string slug, Guid ownerId) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Slug = slug,
        OwnerUserId = ownerId,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static Ingredient Ingredient(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MeasurementUnit = "g"
    };

    private static RecipeService CreateService(FakeRecipeRepository repository, FakeTranslationRepository? translations = null) =>
        new(repository, new FakeIngredientRepository(Tomatoes, Garlic, Pasta), new MeasurementMappingService(), translations ?? new FakeTranslationRepository());

    private sealed class FakeIngredientRepository(params Ingredient[] ingredients) : IIngredientRepository
    {
        public Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Ingredient>>(ingredients);
        public Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Ingredient>>(ingredients.Where(x => ids.Contains(x.Id)).ToDictionary(x => x.Id));
        public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(ingredients.SingleOrDefault(x => x.Id == id));
        public Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(Ingredient ingredient) => throw new NotSupportedException();
        public void Remove(Ingredient ingredient) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRecipeRepository : IRecipeRepository
    {
        public List<Recipe> Recipes { get; } = [];
        public int SaveCount { get; private set; }

        public Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedAsync(
            Guid ownerUserId, int page, int pageSize, string? search, MealSlot? mealType,
            IReadOnlyCollection<Guid>? ingredientIds, CancellationToken cancellationToken)
        {
            var query = Recipes.Where(r => r.OwnerUserId == ownerUserId);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r => r.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
            if (mealType is not null) query = query.Where(r => r.MealType == mealType);
            var requiredIngredientIds = ingredientIds?.Distinct().ToArray() ?? [];
            if (requiredIngredientIds.Length > 0)
                query = query.Where(r => requiredIngredientIds.All(id => r.Ingredients.Any(i => i.IngredientId == id)));
            var filtered = query.ToList();
            var list = filtered.OrderByDescending(r => r.UpdatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult<(IReadOnlyList<Recipe>, int)>((list, filtered.Count));
        }

        public Task<IReadOnlyList<Recipe>> GetRecentAsync(
            Guid ownerUserId, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Recipe>>(Recipes
                .Where(r => r.OwnerUserId == ownerUserId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .Take(limit)
                .ToList());

        public Task<Recipe?> GetByIdAsync(Guid id, Guid ownerUserId, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(Recipes.SingleOrDefault(r => r.Id == id && r.OwnerUserId == ownerUserId));

        public Task<IReadOnlyList<Recipe>> GetAllForDuplicateCheckAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Recipe>>(Recipes.Where(r => r.OwnerUserId == ownerUserId).ToList());

        public Task<IReadOnlyList<Recipe>> GetAllWithIngredientsAsync(Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Recipe>>(Recipes.Where(r => r.HouseId == houseId).ToList());

        public Task<bool> SlugExistsAsync(Guid ownerUserId, string slug, Guid? excludedId, CancellationToken cancellationToken) =>
            Task.FromResult(Recipes.Any(r => r.OwnerUserId == ownerUserId && r.Slug == slug && r.Id != excludedId));

        public void Add(Recipe recipe) => Recipes.Add(recipe);
        public void Remove(Recipe recipe) => Recipes.Remove(recipe);
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
    }

    private sealed class FakeTranslationRepository : ITranslationRepository
    {
        public Dictionary<string, string> Data { get; } = new();

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(Data);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken)
        {
            Data[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken)
        {
            var toRemove = Data.Keys.Where(k => k.StartsWith(keyPrefix)).ToList();
            foreach (var k in toRemove) Data.Remove(k);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
