using Kotlet.Domain.Ingredients;
using Kotlet.Domain.Houses;
using Kotlet.Domain.Recipes;
using Kotlet.Infrastructure.Persistence;
using Kotlet.Infrastructure.Recipes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kotlet.Infrastructure.UnitTests.Recipes;

public sealed class RecipeRepositoryTests
{
    [Fact]
    public async Task GetPagedSummariesAsync_FiltersCaseInsensitivelyAndReturnsIngredientCount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KotletDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new KotletDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var houseId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        dbContext.Houses.Add(new House { Id = houseId, Name = "Test house" });
        dbContext.Ingredients.Add(new Ingredient
        {
            Id = ingredientId,
            Name = "Pasta",
            MeasurementUnit = "g"
        });
        dbContext.Recipes.Add(new Recipe
        {
            Id = recipeId,
            HouseId = houseId,
            OwnerUserId = Guid.NewGuid(),
            Title = "Pasta Primavera",
            Slug = "pasta-primavera",
            Ingredients =
            [
                new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipeId,
                    IngredientId = ingredientId,
                    NormalizedQuantity = Kotlet.Domain.Common.Quantity.FromAmount(250),
                    NormalizedUnit = "g"
                }
            ]
        });
        await dbContext.SaveChangesAsync();

        var repository = new RecipeRepository(dbContext);
        var (items, totalCount) = await repository.GetPagedSummariesAsync(
            houseId, 1, 10, "PRIMAVERA", null, null, CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal(1, totalCount);
        Assert.Equal(recipeId, item.Id);
        Assert.Equal(1, item.IngredientCount);
        Assert.Equal("Pasta Primavera", item.Title);

        var recentItems = await repository.GetRecentSummariesAsync(houseId, 10, CancellationToken.None);
        Assert.Equal(recipeId, Assert.Single(recentItems).Id);

        var (planningItems, _) = await repository.GetPagedAsync(
            houseId, 1, 10, null, null, null, CancellationToken.None);

        var planningItem = Assert.Single(planningItems);
        Assert.Equal("Pasta", planningItem.Ingredients.Single().Ingredient.Name);
    }
}
