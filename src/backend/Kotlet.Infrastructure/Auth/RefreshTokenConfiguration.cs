using Kotlet.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Auth;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id");
        builder.Property(token => token.UserId).HasColumnName("user_id");
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(token => token.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(token => token.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(token => token.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(token => token.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
        builder.Property(token => token.CreatedByIp).HasColumnName("created_by_ip").HasMaxLength(45);
        builder.Property(token => token.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
        builder.HasIndex(token => token.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");
        builder.HasIndex(token => token.UserId).HasDatabaseName("ix_refresh_tokens_user_id");
        builder.HasOne(token => token.User).WithMany(user => user.RefreshTokens).HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(token => token.ReplacedByToken).WithMany().HasForeignKey(token => token.ReplacedByTokenId).OnDelete(DeleteBehavior.Restrict);
    }
}
