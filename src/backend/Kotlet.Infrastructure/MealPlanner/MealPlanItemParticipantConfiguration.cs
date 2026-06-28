using Kotlet.Domain.MealPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.MealPlanner;

internal sealed class MealPlanItemParticipantConfiguration : IEntityTypeConfiguration<MealPlanItemParticipant>
{
    public void Configure(EntityTypeBuilder<MealPlanItemParticipant> builder)
    {
        builder.ToTable("meal_plan_item_participants");
        builder.HasKey(p => new { p.MealPlanItemId, p.UserId });
        builder.Property(p => p.MealPlanItemId).HasColumnName("meal_plan_item_id");
        builder.Property(p => p.UserId).HasColumnName("user_id");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.UserId).HasDatabaseName("ix_meal_plan_item_participants_user_id");
    }
}
