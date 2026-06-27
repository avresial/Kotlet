using Kotlet.Domain.Auth;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.Pantry;
using Kotlet.Domain.Houses;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Persistence;

public sealed class KotletDbContext(DbContextOptions<KotletDbContext> options) : DbContext(options)
{
    public const string DefaultSchema = "kotlet";

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();
    public DbSet<House> Houses => Set<House>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KotletDbContext).Assembly);
    }
}
