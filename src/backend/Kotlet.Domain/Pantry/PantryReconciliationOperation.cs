using Kotlet.Domain.Houses;

namespace Kotlet.Domain.Pantry;

/// <summary>Persists the result and undo information for an idempotent pantry scan.</summary>
public sealed class PantryReconciliationOperation
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public required string OperationId { get; set; }
    public long PantryVersion { get; set; }
    public required string ResponseJson { get; set; }
    public string? UndoToken { get; set; }
    public string? UndoStateJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UndoneAtUtc { get; set; }
    public string? UndoResponseJson { get; set; }
    public House House { get; set; } = null!;
}
