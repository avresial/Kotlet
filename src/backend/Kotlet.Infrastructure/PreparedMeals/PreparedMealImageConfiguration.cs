using Kotlet.Domain.PreparedMeals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.PreparedMeals;

internal sealed class PreparedMealImageConfiguration : IEntityTypeConfiguration<PreparedMealImage>
{
    public void Configure(EntityTypeBuilder<PreparedMealImage> builder)
    {
        builder.ToTable("prepared_meal_images");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).HasColumnName("id");
        builder.Property(image => image.PreparedMealId).HasColumnName("prepared_meal_id");
        builder.Property(image => image.SortOrder).HasColumnName("sort_order");
        builder.HasOne(image => image.Image)
            .WithOne()
            .HasForeignKey<PreparedMealImage>(image => image.Id)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(image => image.PreparedMeal)
            .WithMany(meal => meal.Images)
            .HasForeignKey(image => image.PreparedMealId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(image => new { image.PreparedMealId, image.SortOrder }).IsUnique();
    }
}
