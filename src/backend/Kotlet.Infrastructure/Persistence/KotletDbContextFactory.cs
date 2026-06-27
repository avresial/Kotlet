using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kotlet.Infrastructure.Persistence;

public sealed class KotletDbContextFactory : IDesignTimeDbContextFactory<KotletDbContext>
{
    public KotletDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KotletDbContext>()
            .UseNpgsql("Host=localhost;Database=kotletdb;Username=postgres;Password=postgres")
            .Options;

        return new KotletDbContext(options);
    }
}
