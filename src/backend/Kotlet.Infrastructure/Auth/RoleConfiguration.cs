using Kotlet.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Auth;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).HasColumnName("id");
        builder.Property(role => role.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.HasIndex(role => role.Name).IsUnique().HasDatabaseName("ux_roles_name");
        builder.HasData(
            new Role { Id = RoleIds.User, Name = RoleNames.User },
            new Role { Id = RoleIds.Admin, Name = RoleNames.Admin });
        builder.HasMany(role => role.Users).WithMany(user => user.Roles).UsingEntity("user_roles",
            right => right.HasOne(typeof(User)).WithMany().HasForeignKey("user_id").OnDelete(DeleteBehavior.Cascade),
            left => left.HasOne(typeof(Role)).WithMany().HasForeignKey("role_id").OnDelete(DeleteBehavior.Cascade),
            join =>
            {
                join.ToTable("user_roles");
                join.HasKey("user_id", "role_id");
                join.IndexerProperty<Guid>("user_id").HasColumnName("user_id");
                join.IndexerProperty<Guid>("role_id").HasColumnName("role_id");
            });
    }
}
