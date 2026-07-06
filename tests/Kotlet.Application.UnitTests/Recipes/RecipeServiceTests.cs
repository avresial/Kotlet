using Kotlet.Application.Recipes;
using Kotlet.Application.Ingredients;
using Kotlet.Application.Measurements;
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

        var result = await service.ListAsync(OwnerId, 1, 20, null, null, CancellationToken.None);

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

        var result = await CreateService(repo).ListAsync(OwnerId, 1, 20, null, "breakfast", CancellationToken.None);

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

    private static RecipeService CreateService(FakeRecipeRepository repository) =>
        new(repository, new FakeIngredientRepository(Tomatoes, Garlic, Pasta), new MeasurementMappingService());

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
            Guid ownerUserId, int page, int pageSize, string? search, MealSlot? mealType, CancellationToken cancellationToken)
        {
            var query = Recipes.Where(r => r.OwnerUserId == ownerUserId);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r => r.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
            if (mealType is not null) query = query.Where(r => r.MealType == mealType);
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

        public Task<bool> SlugExistsAsync(Guid ownerUserId, string slug, Guid? excludedId, CancellationToken cancellationToken) =>
            Task.FromResult(Recipes.Any(r => r.OwnerUserId == ownerUserId && r.Slug == slug && r.Id != excludedId));

        public void Add(Recipe recipe) => Recipes.Add(recipe);
        public void Remove(Recipe recipe) => Recipes.Remove(recipe);
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
    }
}
