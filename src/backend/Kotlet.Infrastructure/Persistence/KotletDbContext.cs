using Kotlet.Domain.Auth;
using Kotlet.Domain.Ingredients;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Persistence;

public sealed class KotletDbContext(DbContextOptions<KotletDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KotletDbContext).Assembly);
    }
}
