using Kotlet.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Persistence;

public sealed class KotletDbContext(DbContextOptions<KotletDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KotletDbContext).Assembly);
    }
}
