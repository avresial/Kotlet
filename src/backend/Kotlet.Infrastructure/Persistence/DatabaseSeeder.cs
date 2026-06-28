using Kotlet.Domain.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Kotlet.Domain.Houses;

namespace Kotlet.Infrastructure.Persistence;

public sealed class DatabaseSeeder(
    KotletDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IngredientCsvSeeder ingredientSeeder,
    ILogger<DatabaseSeeder> logger)
{
    private static readonly SeedUser[] Users =
    [
        new("admin@kotlet.local", "Admin123!", "admin"),
        new("testuser@kotlet.local", "TestUser123!", "testUser")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles.ToDictionaryAsync(role => role.Name, cancellationToken);
        var hasDefaultHouse = await dbContext.Houses.AnyAsync(house => house.Id == DefaultHouse.Id, cancellationToken);
        var createDefaultHouse = false;

        foreach (var seedUser in Users)
        {
            var normalizedEmail = seedUser.Email.ToUpperInvariant();
            if (await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
                continue;

            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                DefaultHouseId = DefaultHouse.Id,
                Email = seedUser.Email,
                NormalizedEmail = normalizedEmail,
                PasswordHash = string.Empty,
                DisplayName = seedUser.DisplayName,
                Roles = seedUser.Email == "admin@kotlet.local"
                    ? [roles[RoleNames.User], roles[RoleNames.Admin]]
                    : [roles[RoleNames.User]],
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            user.PasswordHash = passwordHasher.HashPassword(user, seedUser.Password);
            dbContext.Users.Add(user);
            dbContext.HouseMemberships.Add(new HouseMembership { UserId = user.Id, HouseId = DefaultHouse.Id, JoinedAtUtc = now });
            createDefaultHouse = true;
        }

        if (createDefaultHouse && !hasDefaultHouse)
            dbContext.Houses.Add(new House { Id = DefaultHouse.Id, Name = DefaultHouse.Name });

        var createdUserCount = await dbContext.SaveChangesAsync(cancellationToken);
        var createdIngredientCount = await ingredientSeeder.SeedAsync(cancellationToken);
        logger.LogInformation(
            "Development database seeding completed; {UserCount} users and {IngredientCount} ingredients created",
            createdUserCount, createdIngredientCount);
    }

    private sealed record SeedUser(string Email, string Password, string DisplayName);
}
