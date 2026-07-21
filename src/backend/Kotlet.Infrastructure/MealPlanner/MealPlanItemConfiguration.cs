using Kotlet.Domain.Houses;
using Kotlet.Domain.MealPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.MealPlanner;

internal sealed class MealPlanItemConfiguration : IEntityTypeConfiguration<MealPlanItem>
{
    public void Configure(EntityTypeBuilder<MealPlanItem> builder)
    {
        builder.ToTable("meal_plan_items", table => table.HasCheckConstraint(
            "CK_meal_plan_items_one_source",
            "(CASE WHEN recipe_id IS NULL THEN 0 ELSE 1 END + CASE WHEN ingredient_id IS NULL THEN 0 ELSE 1 END + CASE WHEN prepared_meal_id IS NULL THEN 0 ELSE 1 END) = 1"));
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.HouseId).HasColumnName("house_id").IsRequired();
        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(m => m.Date).HasColumnName("planned_date").IsRequired();
        builder.Property(m => m.Slot).HasColumnName("slot").IsRequired();
        builder.Property(m => m.RecipeId).HasColumnName("recipe_id");
        builder.Property(m => m.IngredientId).HasColumnName("ingredient_id");
        builder.Property(m => m.PreparedMealId).HasColumnName("prepared_meal_id");
        builder.Property(m => m.ParentMealPlanItemId).HasColumnName("parent_meal_plan_item_id");
        builder.Property(m => m.IngredientQuantity).HasColumnName("ingredient_quantity");
        builder.Property(m => m.IngredientUnit).HasColumnName("ingredient_unit").HasMaxLength(32);
        builder.Property(m => m.Note).HasColumnName("note").HasColumnType("text");
        builder.Property(m => m.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(m => m.Servings).HasColumnName("servings");
        builder.Property(m => m.Guests).HasColumnName("guests").IsRequired().HasDefaultValue(0);
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Ignore(m => m.EffectiveServings);

        builder.HasMany(m => m.Participants)
            .WithOne(p => p.MealPlanItem)
            .HasForeignKey(p => p.MealPlanItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.PreparedMeal).WithMany().HasForeignKey(m => m.PreparedMealId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.ParentMealPlanItem).WithMany(m => m.AddonItems).HasForeignKey(m => m.ParentMealPlanItemId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.HouseId, m.Date }).HasDatabaseName("ix_meal_plan_items_house_date");
        builder.HasOne<House>().WithMany().HasForeignKey(m => m.HouseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => new { m.UserId, m.Date }).HasDatabaseName("ix_meal_plan_items_user_date");
        builder.HasIndex(m => new { m.UserId, m.Date, m.Slot }).HasDatabaseName("ix_meal_plan_items_user_date_slot");
    }
}
