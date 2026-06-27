using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Persistence;

public sealed class DatabaseSchemaTests
{
    [Fact]
    public void DefaultSchema_UsesCentralDatabaseSchemaConstant()
    {
        Assert.Equal(DatabaseSchemas.Kotlet, KotletDbContext.DefaultSchema);
    }

    [Fact]
    public void EveryApplicationTable_UsesKotletSchema()
    {
        var options = new DbContextOptionsBuilder<KotletDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var dbContext = new KotletDbContext(options);

        var incorrectlyMappedEntities = dbContext.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null && entity.GetSchema() != KotletDbContext.DefaultSchema)
            .Select(entity => $"{entity.ClrType.Name} ({entity.GetSchema() ?? "<default>"}.{entity.GetTableName()})")
            .ToArray();

        Assert.True(incorrectlyMappedEntities.Length == 0,
            $"All application tables must use the '{KotletDbContext.DefaultSchema}' schema. Invalid mappings: {string.Join(", ", incorrectlyMappedEntities)}");
    }
}
