using Kotlet.Domain.Houses;
using Kotlet.Infrastructure.Houses;
using Kotlet.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kotlet.Infrastructure.UnitTests.Houses;

public sealed class HouseRepositoryTests
{
    [Fact]
    public async Task SaveChangesAsync_RetriesHouseWriteWhenPantryVersionChanged()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KotletDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new KotletDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Houses.Add(new House { Id = Guid.NewGuid(), Name = "Test house" });
            await setupContext.SaveChangesAsync();
        }

        await using var houseContext = new KotletDbContext(options);
        await using var pantryContext = new KotletDbContext(options);
        var house = await houseContext.Houses.SingleAsync();
        var pantryHouse = await pantryContext.Houses.SingleAsync();
        house.Name = "Renamed house";
        pantryHouse.PantryVersion++;
        await pantryContext.SaveChangesAsync();

        await new HouseRepository(houseContext).SaveChangesAsync(CancellationToken.None);

        await using var verificationContext = new KotletDbContext(options);
        var savedHouse = await verificationContext.Houses.AsNoTracking().SingleAsync();
        Assert.Equal("Renamed house", savedHouse.Name);
        Assert.Equal(1, savedHouse.PantryVersion);
    }
}
