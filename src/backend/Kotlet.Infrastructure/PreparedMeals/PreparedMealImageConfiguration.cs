using Kotlet.Domain.PreparedMeals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.PreparedMeals;

internal sealed class PreparedMealImageConfiguration : IEntityTypeConfiguration<PreparedMealImage>
{
    public void Configure(EntityTypeBuilder<PreparedMealImage> b)
    {
        b.ToTable("prepared_meal_images"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PreparedMealId).HasColumnName("prepared_meal_id"); b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.HasOne(x => x.Image).WithOne().HasForeignKey<PreparedMealImage>(x => x.Id).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.PreparedMeal).WithMany(x => x.Images).HasForeignKey(x => x.PreparedMealId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.PreparedMealId, x.SortOrder }).IsUnique();
    }
}
