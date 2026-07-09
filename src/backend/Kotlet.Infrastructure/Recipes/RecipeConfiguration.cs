using Kotlet.Domain.Common;
using Kotlet.Domain.Houses;
using Kotlet.Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("recipes");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.HouseId).HasColumnName("house_id").IsRequired();
        builder.Property(r => r.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(r => r.Title).HasColumnName("title").HasMaxLength(160).IsRequired();
        builder.Property(r => r.Slug).HasColumnName("slug").HasMaxLength(200).IsRequired();
        builder.Property(r => r.DescriptionMarkdown).HasColumnName("description_markdown").HasColumnType("text");
        builder.Property(r => r.Servings)
            .HasColumnName("servings")
            .HasConversion(servings => servings.Value, value => ServingCount.FromInt32(value))
            .HasDefaultValue(ServingCount.One)
            .IsRequired();
        builder.Property(r => r.MealType).HasColumnName("meal_type");
        builder.Property(r => r.IsAiAssisted).HasColumnName("is_ai_assisted").HasDefaultValue(false).IsRequired();
        builder.Property(r => r.SourceUrl).HasColumnName("source_url").HasMaxLength(2000);
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasIndex(r => r.OwnerUserId).HasDatabaseName("ix_recipes_owner_user_id");
        builder.HasIndex(r => r.HouseId).HasDatabaseName("ix_recipes_house_id");
        builder.HasOne<House>().WithMany().HasForeignKey(r => r.HouseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => r.UpdatedAtUtc).HasDatabaseName("ix_recipes_updated_at_utc");
        builder.HasIndex(r => new { r.OwnerUserId, r.Slug }).IsUnique().HasDatabaseName("ux_recipes_owner_slug");

        builder.HasMany(r => r.Ingredients)
            .WithOne(i => i.Recipe)
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Images)
            .WithOne(i => i.Recipe)
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
