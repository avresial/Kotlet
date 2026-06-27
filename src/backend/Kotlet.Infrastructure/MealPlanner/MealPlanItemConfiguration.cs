using Kotlet.Domain.MealPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.MealPlanner;

internal sealed class MealPlanItemConfiguration : IEntityTypeConfiguration<MealPlanItem>
{
    public void Configure(EntityTypeBuilder<MealPlanItem> builder)
    {
        builder.ToTable("meal_plan_items");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(m => m.Date).HasColumnName("planned_date").IsRequired();
        builder.Property(m => m.Slot).HasColumnName("slot").IsRequired();
        builder.Property(m => m.RecipeId).HasColumnName("recipe_id");
        builder.Property(m => m.IngredientId).HasColumnName("ingredient_id");
        builder.Property(m => m.Note).HasColumnName("note").HasColumnType("text");
        builder.Property(m => m.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(m => new { m.UserId, m.Date }).HasDatabaseName("ix_meal_plan_items_user_date");
        builder.HasIndex(m => new { m.UserId, m.Date, m.Slot }).HasDatabaseName("ix_meal_plan_items_user_date_slot");
    }
}
