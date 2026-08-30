using Kotlet.Domain.Houses;
using Kotlet.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kotlet.Infrastructure.UnitTests.Shopping;

public sealed class ShoppingListSchemaTests
{
    [Fact]
    public async Task SourceCheckConstraint_RejectsItemsWithoutExactlyOneSource()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KotletDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new KotletDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var houseId = Guid.NewGuid();
        dbContext.Houses.Add(new House { Id = houseId, Name = "Test house" });
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<SqliteException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO shopping_list_items
                (id, house_id, ingredient_id, prepared_meal_id, custom_name, quantity, is_purchased, note)
            VALUES
                ({Guid.NewGuid()}, {houseId}, NULL, NULL, NULL, 1, 0, NULL)
            """));

        Assert.Contains("ck_shopping_list_items_one_source", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
