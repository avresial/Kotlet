using Kotlet.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Auth;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id");
        builder.Property(user => user.DefaultHouseId).HasColumnName("default_house_id");
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(100);
        builder.Property(user => user.PreferredLanguage).HasColumnName("preferred_language").HasMaxLength(2);
        builder.Property(user => user.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(user => user.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(user => user.LastLoginAtUtc).HasColumnName("last_login_at_utc").HasColumnType("timestamp with time zone");
        builder.HasIndex(user => user.NormalizedEmail).IsUnique().HasDatabaseName("ux_users_normalized_email");
        builder.HasIndex(user => user.DefaultHouseId).HasDatabaseName("ix_users_default_house_id");
        builder.HasOne(user => user.DefaultHouse).WithMany().HasForeignKey(user => user.DefaultHouseId).OnDelete(DeleteBehavior.SetNull);
    }
}
