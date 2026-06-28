using Kotlet.Domain.Houses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Houses;

internal sealed class HouseInvitationConfiguration : IEntityTypeConfiguration<HouseInvitation>
{
    public void Configure(EntityTypeBuilder<HouseInvitation> builder)
    {
        builder.ToTable("house_invitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id).HasColumnName("id");
        builder.Property(invitation => invitation.HouseId).HasColumnName("house_id");
        builder.Property(invitation => invitation.InvitedUserId).HasColumnName("invited_user_id");
        builder.Property(invitation => invitation.InvitedByUserId).HasColumnName("invited_by_user_id");
        builder.Property(invitation => invitation.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.HasIndex(invitation => new { invitation.HouseId, invitation.InvitedUserId }).IsUnique().HasDatabaseName("ux_house_invitations_house_user");
        builder.HasIndex(invitation => invitation.InvitedUserId).HasDatabaseName("ix_house_invitations_invited_user_id");
        builder.HasOne(invitation => invitation.House).WithMany(house => house.Invitations).HasForeignKey(invitation => invitation.HouseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(invitation => invitation.InvitedUser).WithMany().HasForeignKey(invitation => invitation.InvitedUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(invitation => invitation.InvitedByUser).WithMany().HasForeignKey(invitation => invitation.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
