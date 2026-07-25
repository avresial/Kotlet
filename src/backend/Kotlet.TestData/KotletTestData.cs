using Kotlet.Domain.Auth;
using Kotlet.Domain.Common;
using Kotlet.Domain.Houses;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Pantry;
using Kotlet.Domain.PreparedMeals;
using Kotlet.Domain.Recipes;
using Kotlet.Domain.Shopping;
using Kotlet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kotlet.TestData;

/// <summary>
/// The one dataset shared by the MCP benchmark, integration tests, browser tests, and local
/// development. Everything here is fixed: identifiers come from <see cref="TestIds"/>, dates
/// from <see cref="Anchor"/>, and the ingredient catalogue from the production seed CSV. Two
/// builds of this fixture produce the same rows, which is what makes measurements comparable
/// and assertions stable.
/// </summary>
public static class KotletTestData
{
    /// <summary>Every timestamp in the fixture derives from this instant.</summary>
    public static readonly DateTimeOffset Anchor = new(2026, 1, 5, 8, 0, 0, TimeSpan.Zero);

    /// <summary>First day of the seeded meal plan. Fourteen consecutive days are populated.</summary>
    public static readonly DateOnly PlanStart = new(2026, 3, 2);

    public const int PlannedDays = 14;
    public const int RecipeCount = 12;
    public const int IngredientsPerRecipe = 6;
    public const int ShoppingItemCount = 12;
    public const int PantryItemCount = 15;

    public static readonly string[] PlannedSlots = ["breakfast", "dinner", "supper"];

    /// <summary>
    /// The household every fixture row belongs to. It reuses the default house id so local
    /// development keeps the household it already had.
    /// </summary>
    public static Guid HouseId => DefaultHouse.Id;

    public const string HouseName = "Test kitchen";

    /// <summary>Accounts the fixture creates. Passwords match the previous development seed.</summary>
    public static readonly TestAccount Owner = new("testuser@kotlet.local", "TestUser123!", "testUser", IsAdmin: false);
    public static readonly TestAccount Admin = new("admin@kotlet.local", "Admin123!", "admin", IsAdmin: true);
    public static readonly TestAccount Housemate = new("housemate@kotlet.local", "Housemate123!", "housemate", IsAdmin: false);

    public static IReadOnlyList<TestAccount> Accounts => [Owner, Admin, Housemate];

    public static string RecipeTitle(int index) => $"Test recipe {index:D2}";
    public static string PreparedMealName(int index) => $"Test prepared meal {index:D2}";

    /// <summary>
    /// Fills an empty database. The caller owns schema creation — run
    /// <c>EnsureCreatedAsync</c> or <c>MigrateAsync</c> first.
    /// </summary>
    public static async Task SeedAsync(KotletDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Houses.AnyAsync(house => house.Id == HouseId, cancellationToken))
            return;

        var ingredients = await SeedIngredientsAsync(dbContext, cancellationToken);
        var users = SeedAccounts(dbContext);
        SeedHouse(dbContext, users);
        await dbContext.SaveChangesAsync(cancellationToken);

        var recipes = SeedRecipes(dbContext, ingredients);
        SeedShoppingList(dbContext, ingredients);
        SeedPantry(dbContext, ingredients);
        var preparedMeals = SeedPreparedMeals(dbContext);
        await dbContext.SaveChangesAsync(cancellationToken);

        SeedMealPlan(dbContext, recipes, preparedMeals, users);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Loads the production ingredient catalogue so search and matching are measured against
    /// realistic data volume rather than a handful of invented rows.
    /// </summary>
    private static async Task<IReadOnlyList<Ingredient>> SeedIngredientsAsync(
        KotletDbContext dbContext, CancellationToken cancellationToken)
    {
        var seeder = new IngredientCsvSeeder(dbContext, NullLogger<IngredientCsvSeeder>.Instance);
        await seeder.SeedAsync(cancellationToken, TestIds.Ingredient);
        return await dbContext.Ingredients.AsNoTracking().OrderBy(ingredient => ingredient.Name)
            .ToListAsync(cancellationToken);
    }

    private static Dictionary<string, User> SeedAccounts(KotletDbContext dbContext)
    {
        var hasher = new PasswordHasher<User>();
        var roles = dbContext.Roles.ToDictionary(role => role.Name);
        var users = new Dictionary<string, User>();

        foreach (var account in Accounts)
        {
            var user = new User
            {
                Id = TestIds.User(account.Email),
                DefaultHouseId = HouseId,
                Email = account.Email,
                NormalizedEmail = account.Email.ToUpperInvariant(),
                PasswordHash = string.Empty,
                DisplayName = account.DisplayName,
                Roles = account.IsAdmin
                    ? [roles[RoleNames.User], roles[RoleNames.Admin]]
                    : [roles[RoleNames.User]],
                CreatedAtUtc = Anchor.UtcDateTime,
                UpdatedAtUtc = Anchor.UtcDateTime
            };
            user.PasswordHash = hasher.HashPassword(user, account.Password);
            dbContext.Users.Add(user);
            users[account.Email] = user;
        }

        return users;
    }

    private static void SeedHouse(KotletDbContext dbContext, Dictionary<string, User> users)
    {
        dbContext.Houses.Add(new House { Id = HouseId, Name = HouseName });
        foreach (var user in users.Values)
            dbContext.HouseMemberships.Add(new HouseMembership
            {
                UserId = user.Id,
                HouseId = HouseId,
                JoinedAtUtc = Anchor.UtcDateTime
            });
    }

    private static List<Recipe> SeedRecipes(KotletDbContext dbContext, IReadOnlyList<Ingredient> ingredients)
    {
        var ownerId = TestIds.User(Owner.Email);
        var recipes = new List<Recipe>(RecipeCount);

        for (var index = 0; index < RecipeCount; index++)
        {
            var title = RecipeTitle(index);
            var recipe = new Recipe
            {
                Id = TestIds.Recipe(title),
                HouseId = HouseId,
                OwnerUserId = ownerId,
                Title = title,
                Slug = Slug(title),
                DescriptionMarkdown = Description(index),
                Servings = ServingCount.FromInt32(2 + index % 4),
                MealType = (MealSlot)(1 + index % 3),
                IsAiAssisted = index % 4 == 0,
                SourceUrl = index % 4 == 0 ? $"https://example.com/test-recipe-{index:D2}" : null,
                CreatedAtUtc = Anchor.AddDays(index),
                UpdatedAtUtc = Anchor.AddDays(index)
            };

            for (var slot = 0; slot < IngredientsPerRecipe; slot++)
            {
                // Walking the catalogue with a per-recipe stride gives each recipe a different
                // ingredient set without hard-coding names that a CSV edit could remove.
                var ingredient = ingredients[(index * IngredientsPerRecipe + slot * 7) % ingredients.Count];
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Id = TestIds.RecipeIngredient($"{title}/{slot}"),
                    RecipeId = recipe.Id,
                    IngredientId = ingredient.Id,
                    SortOrder = slot,
                    NormalizedQuantity = Quantity.FromAmount(50m + slot * 25m),
                    NormalizedUnit = ingredient.MeasurementUnit,
                    Note = slot == 0 ? "finely chopped" : null
                });
            }

            dbContext.Recipes.Add(recipe);
            recipes.Add(recipe);
        }

        return recipes;
    }

    private static void SeedShoppingList(KotletDbContext dbContext, IReadOnlyList<Ingredient> ingredients)
    {
        for (var index = 0; index < ShoppingItemCount; index++)
        {
            var ingredient = ingredients[index * 11 % ingredients.Count];
            dbContext.ShoppingListItems.Add(new ShoppingListItem
            {
                Id = TestIds.ShoppingItem(ingredient.Name),
                HouseId = HouseId,
                IngredientId = ingredient.Id,
                Quantity = Quantity.FromAmount(100m + index * 50m),
                IsPurchased = index % 4 == 0,
                Note = index % 3 == 0 ? "any brand" : null
            });
        }
    }

    private static void SeedPantry(KotletDbContext dbContext, IReadOnlyList<Ingredient> ingredients)
    {
        for (var index = 0; index < PantryItemCount; index++)
        {
            var ingredient = ingredients[(index * 13 + 5) % ingredients.Count];
            dbContext.PantryItems.Add(new PantryItem
            {
                Id = TestIds.PantryItem(ingredient.Name),
                HouseId = HouseId,
                IngredientId = ingredient.Id,
                Quantity = Quantity.FromAmount(200m + index * 40m),
                ExpirationDate = index % 3 == 0 ? PlanStart.AddDays(20 + index) : null,
                StorageLocation = (StorageLocation)(1 + index % 3)
            });
        }
    }

    private static List<PreparedMeal> SeedPreparedMeals(KotletDbContext dbContext)
    {
        var meals = new List<PreparedMeal>(3);
        for (var index = 0; index < 3; index++)
        {
            var name = PreparedMealName(index);
            var meal = new PreparedMeal
            {
                Id = TestIds.PreparedMeal(name),
                HouseId = HouseId,
                Name = name,
                Description = "A freezer meal used to size prepared-meal payloads.",
                Brand = "Test Foods",
                Store = "Test market",
                Category = "Dinner",
                PackageQuantity = 400m,
                PackageUnit = "g",
                Servings = 2,
                CaloriesPerServing = 350m + index * 25m,
                Price = 12.50m + index,
                PreparationInstructions = "Heat for 8 minutes.",
                CreatedAtUtc = Anchor,
                UpdatedAtUtc = Anchor
            };
            dbContext.PreparedMeals.Add(meal);
            meals.Add(meal);
        }

        return meals;
    }

    private static void SeedMealPlan(
        KotletDbContext dbContext,
        IReadOnlyList<Recipe> recipes,
        IReadOnlyList<PreparedMeal> preparedMeals,
        Dictionary<string, User> users)
    {
        var ownerId = TestIds.User(Owner.Email);
        var housemateId = TestIds.User(Housemate.Email);

        for (var day = 0; day < PlannedDays; day++)
            for (var slotIndex = 0; slotIndex < PlannedSlots.Length; slotIndex++)
            {
                var date = PlanStart.AddDays(day);
                var key = $"{date:yyyy-MM-dd}/{PlannedSlots[slotIndex]}";
                var position = day * PlannedSlots.Length + slotIndex;

                var item = new MealPlanItem
                {
                    Id = TestIds.MealPlanItem(key),
                    HouseId = HouseId,
                    UserId = ownerId,
                    Date = date,
                    Slot = SlotFor(PlannedSlots[slotIndex]),
                    SortOrder = 0,
                    // Every seventh meal is a prepared meal so both meal kinds are represented.
                    RecipeId = position % 7 == 6 ? null : recipes[position % recipes.Count].Id,
                    PreparedMealId = position % 7 == 6 ? preparedMeals[position % preparedMeals.Count].Id : null,
                    Guests = position % 5 == 0 ? 2 : 0,
                    Note = position % 6 == 0 ? "leftovers go in the freezer" : null,
                    CreatedAt = Anchor,
                    UpdatedAt = Anchor
                };

                item.Participants.Add(new MealPlanItemParticipant { UserId = ownerId, PortionPercent = 100 });
                if (position % 2 == 0)
                    item.Participants.Add(new MealPlanItemParticipant
                    {
                        UserId = housemateId,
                        PortionPercent = position % 4 == 0 ? 75 : 100
                    });

                dbContext.MealPlanItems.Add(item);
            }

        _ = users;
    }

    private static MealSlot SlotFor(string slot) => slot switch
    {
        "breakfast" => MealSlot.Breakfast,
        "second-breakfast" => MealSlot.SecondBreakfast,
        "dinner" => MealSlot.Dinner,
        "snack" => MealSlot.Snack,
        "supper" => MealSlot.Supper,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown meal slot.")
    };

    private static string Slug(string title) =>
        string.Concat(title.ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : '-'));

    private static string Description(int index) =>
        string.Join('\n', Enumerable.Range(1, 8).Select(step =>
            $"{step}. Combine the ingredients for test recipe {index:D2} and cook them until step {step} is done."));
}

public sealed record TestAccount(string Email, string Password, string DisplayName, bool IsAdmin);
