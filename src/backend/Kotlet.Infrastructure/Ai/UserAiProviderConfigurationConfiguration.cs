using Kotlet.Domain.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Ai;

internal sealed class UserAiProviderConfigurationConfiguration : IEntityTypeConfiguration<UserAiProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<UserAiProviderConfiguration> builder)
    {
        builder.ToTable("user_ai_provider_configurations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.ProviderName).HasColumnName("provider_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.BaseUrl).HasColumnName("base_url").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.ApiKey).HasColumnName("api_key").HasMaxLength(4096);
        builder.Property(x => x.DefaultModel).HasColumnName("default_model").HasMaxLength(200);
        builder.Property(x => x.Models).HasColumnName("models").HasMaxLength(2000);
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ux_user_ai_provider_configurations_user_id");
        builder.HasOne(x => x.User).WithOne(x => x.AiProviderConfiguration).HasForeignKey<UserAiProviderConfiguration>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
