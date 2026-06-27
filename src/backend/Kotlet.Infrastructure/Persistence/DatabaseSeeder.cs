using Kotlet.Domain.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kotlet.Infrastructure.Persistence;

public sealed class DatabaseSeeder(
    KotletDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    ILogger<DatabaseSeeder> logger)
{
    private static readonly SeedUser[] Users =
    [
        new("admin@kotlet.local", "Admin123!", "admin"),
        new("testuser@kotlet.local", "TestUser123!", "testUser")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var seedUser in Users)
        {
            var normalizedEmail = seedUser.Email.ToUpperInvariant();
            if (await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
                continue;

            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = seedUser.Email,
                NormalizedEmail = normalizedEmail,
                PasswordHash = string.Empty,
                DisplayName = seedUser.DisplayName,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            user.PasswordHash = passwordHasher.HashPassword(user, seedUser.Password);
            dbContext.Users.Add(user);
        }

        var createdCount = await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Development database seeding completed; {CreatedCount} users created", createdCount);
    }

    private sealed record SeedUser(string Email, string Password, string DisplayName);
}
