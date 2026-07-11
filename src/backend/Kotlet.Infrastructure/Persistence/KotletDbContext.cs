using Kotlet.Domain.Auth;
using Kotlet.Domain.Ai;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Pantry;
using Kotlet.Domain.Houses;
using Kotlet.Domain.Recipes;
using Kotlet.Domain.Shopping;
using Kotlet.Domain.Sources;
using Kotlet.Domain.Translations;
using Kotlet.Domain.FoodSettings;
using Kotlet.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kotlet.Infrastructure.Persistence;

public sealed class KotletDbContext(DbContextOptions<KotletDbContext> options) : DbContext(options)
{
    public const string DefaultSchema = DatabaseSchemas.Kotlet;

    public DbSet<User> Users => Set<User>();
    public DbSet<UserAiProviderConfiguration> UserAiProviderConfigurations => Set<UserAiProviderConfiguration>();
    public DbSet<UserFoodSettings> UserFoodSettings => Set<UserFoodSettings>();
    public DbSet<UserExcludedIngredient> UserExcludedIngredients => Set<UserExcludedIngredient>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();
    public DbSet<House> Houses => Set<House>();
    public DbSet<HouseMembership> HouseMemberships => Set<HouseMembership>();
    public DbSet<HouseInvitation> HouseInvitations => Set<HouseInvitation>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
    public DbSet<RecipeImportJob> RecipeImportJobs => Set<RecipeImportJob>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<RecipeSource> RecipeSources => Set<RecipeSource>();
    public DbSet<RecipeImageSource> RecipeImageSources => Set<RecipeImageSource>();
    public DbSet<MealPlanItem> MealPlanItems => Set<MealPlanItem>();
    public DbSet<MealPlanItemParticipant> MealPlanItemParticipants => Set<MealPlanItemParticipant>();
    public DbSet<Translation> Translations => Set<Translation>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KotletDbContext).Assembly);

        // SQLite does not support DateTimeOffset in ORDER BY. Store as UTC ticks (long)
        // so sorting works correctly. PostgreSQL is unaffected by this branch.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(
                v => v.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));
            var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
                v => v == null ? null : (long?)v.Value.UtcTicks,
                v => v == null ? null : (DateTimeOffset?)new DateTimeOffset(v.Value, TimeSpan.Zero));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                        property.SetValueConverter(dateTimeOffsetConverter);
                    else if (property.ClrType == typeof(DateTimeOffset?))
                        property.SetValueConverter(nullableDateTimeOffsetConverter);
                }
            }
        }
    }
}
