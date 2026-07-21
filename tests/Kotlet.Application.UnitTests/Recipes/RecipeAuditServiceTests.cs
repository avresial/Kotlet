using Kotlet.Application.Recipes;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Recipes;
using Xunit;

namespace Kotlet.Application.UnitTests.Recipes;

public sealed class RecipeAuditServiceTests
{
    private static readonly Guid HouseId = Guid.NewGuid();

    [Fact]
    public async Task Audit_CompleteRecipe_IsNotReported()
    {
        var recipe = CreateRecipe("Complete", description: "Steps", withIngredient: true, mealType: MealSlot.Dinner);
        var service = CreateService([recipe], recipesWithImage: [recipe.Id]);

        var items = await service.ListRecipesRequiringFixAsync(HouseId, 5, CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Audit_MissingIngredientsOrDescription_IsImportant()
    {
        var recipe = CreateRecipe("Bare", description: " ", withIngredient: false, mealType: MealSlot.Dinner);
        var service = CreateService([recipe], recipesWithImage: [recipe.Id]);

        var item = Assert.Single(await service.ListRecipesRequiringFixAsync(HouseId, 5, CancellationToken.None));

        Assert.Equal(RecipeAuditImportance.Important, item.Importance);
        Assert.Equal([RecipeAuditElements.Ingredients, RecipeAuditElements.Description], item.MissingElements);
        Assert.Equal(2, item.MissingCount);
    }

    [Fact]
    public async Task Audit_MissingImageOrMealType_IsMinor()
    {
        var recipe = CreateRecipe("No photo", description: "Steps", withIngredient: true, mealType: null);
        var service = CreateService([recipe], recipesWithImage: []);

        var item = Assert.Single(await service.ListRecipesRequiringFixAsync(HouseId, 5, CancellationToken.None));

        Assert.Equal(RecipeAuditImportance.Minor, item.Importance);
        Assert.Equal([RecipeAuditElements.Image, RecipeAuditElements.MealType], item.MissingElements);
    }

    [Fact]
    public async Task Audit_OrdersByImportanceThenMissingCountThenTitle()
    {
        var minorOne = CreateRecipe("A minor single", description: "Steps", withIngredient: true, mealType: null);
        var importantSmall = CreateRecipe("Important small", description: null, withIngredient: true, mealType: MealSlot.Dinner);
        var importantBig = CreateRecipe("Important big", description: null, withIngredient: false, mealType: null);
        var minorTwo = CreateRecipe("B minor single", description: "Steps", withIngredient: true, mealType: null);
        var service = CreateService(
            [minorOne, importantSmall, importantBig, minorTwo],
            recipesWithImage: [minorOne.Id, importantSmall.Id, minorTwo.Id]);

        var items = await service.ListRecipesRequiringFixAsync(HouseId, 5, CancellationToken.None);

        Assert.Equal(
            [importantBig.Id, importantSmall.Id, minorOne.Id, minorTwo.Id],
            items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public async Task Audit_RespectsLimit()
    {
        var recipes = Enumerable.Range(0, 8)
            .Select(i => CreateRecipe($"Recipe {i}", description: null, withIngredient: false, mealType: null))
            .ToArray();
        var service = CreateService(recipes, recipesWithImage: []);

        var items = await service.ListRecipesRequiringFixAsync(HouseId, 5, CancellationToken.None);

        Assert.Equal(5, items.Count);
    }

    [Fact]
    public async Task Audit_WithoutImageRepository_DoesNotReportImages()
    {
        var recipe = CreateRecipe("No image repo", description: "Steps", withIngredient: true, mealType: MealSlot.Dinner);
        var repository = new FakeRecipeRepository([recipe]);
        var service = new RecipeAuditService(repository);

        var items = await service.ListRecipesRequiringFixAsync(HouseId, 5, CancellationToken.None);

        Assert.Empty(items);
    }

    private static RecipeAuditService CreateService(Recipe[] recipes, Guid[] recipesWithImage) =>
        new(new FakeRecipeRepository(recipes), new FakeImageRepository(recipesWithImage));

    private static Recipe CreateRecipe(string title, string? description, bool withIngredient, MealSlot? mealType)
    {
        var id = Guid.NewGuid();
        return new Recipe
        {
            Id = id,
            HouseId = HouseId,
            OwnerUserId = Guid.NewGuid(),
            Title = title,
            Slug = title.ToLowerInvariant().Replace(' ', '-'),
            DescriptionMarkdown = description,
            MealType = mealType,
            Ingredients = withIngredient
                ? [new RecipeIngredient { RecipeId = id, IngredientId = Guid.NewGuid(), NormalizedUnit = "g" }]
                : []
        };
    }

    private sealed class FakeRecipeRepository(Recipe[] recipes) : IRecipeRepository
    {
        public Task<IReadOnlyList<Recipe>> GetAllWithIngredientsAsync(Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Recipe>>(recipes.Where(r => r.HouseId == houseId).ToList());

        public Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedAsync(
            Guid ownerUserId, int page, int pageSize, string? search, MealSlot? mealType,
            IReadOnlyCollection<Guid>? ingredientIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Recipe>> GetRecentAsync(Guid ownerUserId, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Recipe?> GetByIdAsync(Guid id, Guid ownerUserId, bool tracked, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Recipe?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Recipe>> GetAllForDuplicateCheckAsync(Guid ownerUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(Guid ownerUserId, string slug, Guid? excludedId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(Recipe recipe) => throw new NotSupportedException();
        public void Remove(Recipe recipe) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeImageRepository(Guid[] recipesWithImage) : IRecipeImageRepository
    {
        public Task<IReadOnlyDictionary<Guid, Guid>> GetFirstImageIdsAsync(IReadOnlyList<Guid> recipeIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(recipeIds
                .Where(recipesWithImage.Contains)
                .ToDictionary(id => id, _ => Guid.NewGuid()));

        public Task<bool> RecipeExistsAsync(Guid recipeId, Guid ownerUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountAsync(Guid recipeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<RecipeImage>> ListAsync(Guid recipeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RecipeImage?> GetAsync(Guid recipeId, Guid imageId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateSortOrdersAsync(Guid recipeId, IReadOnlyList<Guid> imageIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(RecipeImage image) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
