using Kotlet.Domain.Pantry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Pantry;

internal sealed class PantryReconciliationOperationConfiguration : IEntityTypeConfiguration<PantryReconciliationOperation>
{
    public void Configure(EntityTypeBuilder<PantryReconciliationOperation> builder)
    {
        builder.ToTable("pantry_reconciliation_operations");
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Id).HasColumnName("id");
        builder.Property(operation => operation.HouseId).HasColumnName("house_id").IsRequired();
        builder.Property(operation => operation.OperationId).HasColumnName("operation_id").HasMaxLength(200).IsRequired();
        builder.Property(operation => operation.PantryVersion).HasColumnName("pantry_version").IsRequired();
        builder.Property(operation => operation.ResponseJson).HasColumnName("response_json").HasColumnType("text").IsRequired();
        builder.Property(operation => operation.UndoToken).HasColumnName("undo_token").HasMaxLength(100);
        builder.Property(operation => operation.UndoStateJson).HasColumnName("undo_state_json").HasColumnType("text");
        builder.Property(operation => operation.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(operation => operation.UndoneAtUtc).HasColumnName("undone_at_utc");
        builder.Property(operation => operation.UndoResponseJson).HasColumnName("undo_response_json").HasColumnType("text");
        builder.HasIndex(operation => new { operation.HouseId, operation.OperationId })
            .IsUnique()
            .HasDatabaseName("ux_pantry_reconciliation_operations_house_operation");
        builder.HasIndex(operation => operation.UndoToken)
            .IsUnique()
            .HasDatabaseName("ux_pantry_reconciliation_operations_undo_token");
        builder.HasOne(operation => operation.House)
            .WithMany()
            .HasForeignKey(operation => operation.HouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
