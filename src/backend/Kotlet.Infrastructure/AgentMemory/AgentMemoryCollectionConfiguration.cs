using Kotlet.Domain.AgentMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.AgentMemory;

internal sealed class AgentMemoryCollectionConfiguration : IEntityTypeConfiguration<AgentMemoryCollection>
{
    public void Configure(EntityTypeBuilder<AgentMemoryCollection> builder)
    {
        builder.ToTable("agent_memory_collections");
        builder.HasKey(collection => new { collection.UserId, collection.HouseId });
        builder.Property(collection => collection.UserId).HasColumnName("user_id");
        builder.Property(collection => collection.HouseId).HasColumnName("house_id");
        builder.Property(collection => collection.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.HasOne<Kotlet.Domain.Auth.User>().WithMany().HasForeignKey(collection => collection.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Kotlet.Domain.Houses.House>().WithMany().HasForeignKey(collection => collection.HouseId).OnDelete(DeleteBehavior.Cascade);
    }
}
