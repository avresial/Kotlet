using Kotlet.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

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
        Assert.Empty(await dbContext.Ingredients
            .Where(x => x.MeasurementUnit != "g" && x.MeasurementUnit != "ml")
            .ToListAsync());
    }
}
