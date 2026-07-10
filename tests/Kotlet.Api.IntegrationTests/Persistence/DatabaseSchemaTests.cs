using Kotlet.Domain.Recipes;
using Kotlet.Domain.Sources;
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

    [Theory]
    [InlineData(typeof(Source), "sources")]
    [InlineData(typeof(RecipeSource), "recipe_sources")]
    [InlineData(typeof(RecipeImageSource), "recipe_image_sources")]
    public void SourceEntities_MapToExpectedTables(Type entityType, string expectedTableName)
    {
        var options = new DbContextOptionsBuilder<KotletDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var dbContext = new KotletDbContext(options);

        var entity = dbContext.Model.FindEntityType(entityType);

        Assert.NotNull(entity);
        Assert.Equal(expectedTableName, entity.GetTableName());
        Assert.Equal(KotletDbContext.DefaultSchema, entity.GetSchema());
    }
}
