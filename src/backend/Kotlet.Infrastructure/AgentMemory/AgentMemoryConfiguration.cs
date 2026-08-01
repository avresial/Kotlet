using Kotlet.Domain.AgentMemory;
using AgentMemoryEntity = Kotlet.Domain.AgentMemory.AgentMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.AgentMemory;

internal sealed class AgentMemoryConfiguration : IEntityTypeConfiguration<AgentMemoryEntity>
{
    public void Configure(EntityTypeBuilder<AgentMemoryEntity> builder)
    {
        builder.ToTable("agent_memories");
        builder.HasKey(memory => memory.Id);
        builder.Property(memory => memory.Id).HasColumnName("id");
        builder.Property(memory => memory.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(memory => memory.HouseId).HasColumnName("house_id").IsRequired();
        builder.Property(memory => memory.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(memory => memory.Content).HasColumnName("content").HasMaxLength(10_000).IsRequired();
        builder.Property(memory => memory.Category).HasColumnName("category").HasMaxLength(100);
        builder.Property(memory => memory.Source).HasColumnName("source").IsRequired();
        builder.Property(memory => memory.Confidence).HasColumnName("confidence").HasPrecision(5, 4);
        builder.Property(memory => memory.ReviewStatus).HasColumnName("review_status").IsRequired();
        builder.Property(memory => memory.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(memory => memory.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(memory => memory.ExpiresAt).HasColumnName("expires_at");
        builder.Property(memory => memory.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Property(memory => memory.Revision).HasColumnName("revision").IsRequired();
        builder.Property(memory => memory.CreatedByClient).HasColumnName("created_by_client").HasMaxLength(200);
        builder.Property(memory => memory.UpdatedByClient).HasColumnName("updated_by_client").HasMaxLength(200);
        builder.Property(memory => memory.IsDeleted).HasColumnName("is_deleted").IsRequired().HasDefaultValue(false);
        builder.Property(memory => memory.DeletedAt).HasColumnName("deleted_at");
        builder.HasIndex(memory => new { memory.UserId, memory.HouseId, memory.Revision })
            .HasDatabaseName("ix_agent_memories_scope_revision");
        builder.HasIndex(memory => new { memory.UserId, memory.HouseId, memory.IsDeleted })
            .HasDatabaseName("ix_agent_memories_scope_deleted");
        builder.HasOne<Kotlet.Domain.Auth.User>().WithMany().HasForeignKey(memory => memory.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Kotlet.Domain.Houses.House>().WithMany().HasForeignKey(memory => memory.HouseId).OnDelete(DeleteBehavior.Cascade);
    }
}
