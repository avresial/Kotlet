using Kotlet.Domain.Pantry;

namespace Kotlet.Application.Pantry;

public sealed record PantryObservation(
    string ObservationId,
    string RawPhrase,
    string? NormalizedName = null,
    decimal? EstimatedQuantity = null,
    string? Unit = null,
    decimal IdentityConfidence = 1m,
    decimal? QuantityConfidence = null,
    IReadOnlyList<string>? Evidence = null,
    string? PackageDescription = null);

public sealed record PantryResolveObservationsRequest(
    IReadOnlyList<PantryObservation> Observations,
    int UnrecognizedCount = 0,
    string? Locale = null);

public sealed record PantryResolutionCandidate(
    Guid ItemId,
    string ItemType,
    string Name,
    string MeasurementUnit,
    decimal MatchConfidence);

public sealed record PantryMatchedObservation(
    string ObservationId,
    string RawPhrase,
    Guid ItemId,
    string ItemType,
    string MatchedName,
    string MeasurementUnit,
    string MatchType,
    decimal MatchConfidence,
    decimal IdentityConfidence);

public sealed record PantryAmbiguousObservation(
    string ObservationId,
    string RawPhrase,
    string NormalizedPhrase,
    IReadOnlyList<PantryResolutionCandidate> Candidates,
    decimal IdentityConfidence);

public sealed record PantryUnmatchedObservation(
    string ObservationId,
    string RawPhrase,
    string NormalizedPhrase,
    string Reason,
    decimal IdentityConfidence,
    IReadOnlyList<Guid>? CandidateIds = null,
    decimal? RecognitionConfidence = null);

public sealed record PantryObservationResolutionResponse(
    string Status,
    long PantryVersion,
    IReadOnlyList<PantryMatchedObservation> Matched,
    IReadOnlyList<PantryAmbiguousObservation> Ambiguous,
    IReadOnlyList<PantryUnmatchedObservation> Unmatched,
    int UnrecognizedCount,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null);

public sealed record PantryReconciliationScope(
    string? Location = null,
    string Coverage = "partial");

public sealed record PantryReconcileItem(
    string ObservationId,
    Guid ItemId,
    string ItemType,
    decimal? ObservedQuantity,
    string? ObservedUnit,
    decimal? NormalizedQuantity,
    string? NormalizedUnit,
    decimal QuantityConfidence,
    decimal IdentityConfidence = 1m,
    string? PackageDescription = null,
    decimal? ConversionConfidence = null);

public sealed record PantryReconcileRequest(
    string OperationId,
    long? ExpectedPantryVersion,
    string Mode,
    PantryReconciliationScope Scope,
    IReadOnlyList<PantryReconcileItem> Items,
    IReadOnlyList<PantryUnmatchedObservation>? Unmatched = null,
    IReadOnlyList<PantryAmbiguousObservation>? Ambiguous = null,
    int UnrecognizedCount = 0,
    string? Locale = null,
    bool Confirm = false,
    bool ConfirmDestructiveChanges = false,
    bool Confirmed = false);

public sealed record PantryDiffEntry(
    Guid ItemId,
    string ItemType,
    Guid? PantryItemId,
    string ItemName,
    string MeasurementUnit,
    decimal? PreviousQuantity,
    string? PreviousUnit,
    decimal? NewQuantity,
    string? NewUnit,
    decimal? ChangeQuantity,
    IReadOnlyList<string> ObservationIds,
    decimal? ObservedQuantity,
    string? ObservedUnit,
    decimal? ConversionConfidence,
    string? PackageDescription);

public sealed record PantryReviewEntry(
    string ObservationId,
    Guid? ItemId,
    string? ItemName,
    string Reason,
    decimal? ObservedQuantity,
    string? ObservedUnit,
    decimal? NormalizedQuantity,
    string? NormalizedUnit,
    decimal QuantityConfidence,
    decimal IdentityConfidence,
    string? PackageDescription);

public sealed record PantryReconciliationResponse(
    string Status,
    long PantryVersion,
    IReadOnlyList<PantryDiffEntry> Added,
    IReadOnlyList<PantryDiffEntry> Increased,
    IReadOnlyList<PantryDiffEntry> Decreased,
    IReadOnlyList<PantryDiffEntry> Removed,
    IReadOnlyList<PantryDiffEntry> Unchanged,
    IReadOnlyList<PantryReviewEntry> NeedsReview,
    IReadOnlyList<PantryUnmatchedObservation> Unmatched,
    IReadOnlyList<PantryAmbiguousObservation> Ambiguous,
    int UnrecognizedCount,
    string? UndoToken,
    string UiResource,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null);

public sealed record PantryUndoItem(
    Guid PantryItemId,
    Guid IngredientId,
    decimal Quantity,
    DateOnly? ExpirationDate,
    StorageLocation? StorageLocation,
    decimal? LastObservedQuantity,
    string? LastObservedUnit,
    string? PackageDescription,
    decimal? ConversionConfidence,
    DateTimeOffset? LastObservedAtUtc,
    string? LastObservationIdsJson);

public sealed record PantryUndoState(
    IReadOnlyList<PantryUndoItem> PreviousItems,
    IReadOnlyList<Guid> AddedPantryItemIds);

public sealed record PantryUndoResponse(
    string Status,
    long PantryVersion,
    string? UndoToken,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null);
