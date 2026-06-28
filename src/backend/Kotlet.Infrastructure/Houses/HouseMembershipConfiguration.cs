using Kotlet.Domain.Houses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Houses;

internal sealed class HouseMembershipConfiguration : IEntityTypeConfiguration<HouseMembership>
{
    public void Configure(EntityTypeBuilder<HouseMembership> builder)
    {
        builder.ToTable("house_memberships");
        builder.HasKey(membership => new { membership.UserId, membership.HouseId });
        builder.Property(membership => membership.UserId).HasColumnName("user_id");
        builder.Property(membership => membership.HouseId).HasColumnName("house_id");
        builder.Property(membership => membership.JoinedAtUtc).HasColumnName("joined_at_utc").HasColumnType("timestamp with time zone");
        builder.HasIndex(membership => membership.HouseId).HasDatabaseName("ix_house_memberships_house_id");
        builder.HasOne(membership => membership.User).WithMany(user => user.Memberships).HasForeignKey(membership => membership.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(membership => membership.House).WithMany(house => house.Memberships).HasForeignKey(membership => membership.HouseId).OnDelete(DeleteBehavior.Cascade);
    }
}
