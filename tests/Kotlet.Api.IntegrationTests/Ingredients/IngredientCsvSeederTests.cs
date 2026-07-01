using Kotlet.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Kotlet.Domain.Ingredients;

namespace Kotlet.Api.IntegrationTests.Ingredients;

public sealed class IngredientCsvSeederTests
{
    [Fact]
    public async Task Seed_LoadsCsvAndIsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KotletDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new KotletDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var seeder = new IngredientCsvSeeder(dbContext, NullLogger<IngredientCsvSeeder>.Instance);

        var firstCount = await seeder.SeedAsync(CancellationToken.None);
        var secondCount = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(323, firstCount);
        Assert.Equal(0, secondCount);
        Assert.Equal(323, await dbContext.Ingredients.CountAsync());
        var orange = await dbContext.Ingredients.SingleAsync(x => x.Name == "Orange");
        Assert.True(orange.IsCountable);
        Assert.Equal(150m, orange.MeasurementUnitsPerPiece);
        var salmon = await dbContext.Ingredients.SingleAsync(x => x.Name == "Salmon fillet");
        Assert.Equal(FoodCategory.Fish, salmon.Category);
        Assert.True(salmon.Allergens.HasFlag(Allergen.Fish));
        var parmesan = await dbContext.Ingredients.SingleAsync(x => x.Name == "Parmesan");
        Assert.Equal(FoodCategory.Cheese, parmesan.Category);
        Assert.Equal(DietarySuitability.None, parmesan.Suitability);
        Assert.Empty(await dbContext.Ingredients
            .Where(x => x.MeasurementUnit != "g" && x.MeasurementUnit != "ml")
            .ToListAsync());
    }

    [Fact]
    public async Task Seed_SkipsWhenAnyIngredientExists()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KotletDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new KotletDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Ingredients.Add(new Ingredient
        {
            Id = Guid.NewGuid(), Name = "Custom", MeasurementUnit = "g"
        });
        await dbContext.SaveChangesAsync();

        var count = await new IngredientCsvSeeder(dbContext, NullLogger<IngredientCsvSeeder>.Instance)
            .SeedAsync(CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Single(await dbContext.Ingredients.ToListAsync());
    }

    [Fact]
    public async Task Flags_RoundTripAndCanBeQueried()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KotletDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new KotletDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var expected = Allergen.Milk | Allergen.Gluten;
        dbContext.Ingredients.Add(new Ingredient { Id = Guid.NewGuid(), Name = "Test", MeasurementUnit = "g", Allergens = expected });
        await dbContext.SaveChangesAsync();

        Assert.Equal(expected, (await dbContext.Ingredients.SingleAsync()).Allergens);
        Assert.Single(await dbContext.Ingredients.Where(x => (x.Allergens & Allergen.Milk) != 0).ToListAsync());
    }
}
