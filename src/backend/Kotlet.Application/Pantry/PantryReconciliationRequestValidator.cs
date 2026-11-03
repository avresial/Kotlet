using Kotlet.Domain.Pantry;

namespace Kotlet.Application.Pantry;

/// <summary>Validates resolve and reconcile requests before any catalogue or pantry access.</summary>
internal static class PantryReconciliationRequestValidator
{
    internal const string IngredientType = "ingredient";

    private const decimal MaximumQuantity = 99999999.999m;
    private const int MaximumObservations = 200;
    private const int MaximumUnrecognizedCount = 1000;

    public static Dictionary<string, string[]> ValidateResolution(
        IReadOnlyList<PantryObservation> observations,
        int unrecognizedCount)
    {
        var errors = new Dictionary<string, string[]>();
        if (observations.Count == 0 && unrecognizedCount == 0)
        {
            errors["observations"] = ["Provide at least one observation or a positive unrecognizedCount."];
        }
        else if (observations.Count > MaximumObservations)
        {
            errors["observations"] = [$"At most {MaximumObservations} observations are allowed."];
        }
        if (unrecognizedCount is < 0 or > MaximumUnrecognizedCount)
        {
            errors["unrecognizedCount"] = [$"Unrecognized count must be between 0 and {MaximumUnrecognizedCount}."];
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < observations.Count; index++)
        {
            var observation = observations[index];
            if (string.IsNullOrWhiteSpace(observation.ObservationId) || observation.ObservationId.Trim().Length > 100)
            {
                errors[$"observations[{index}].observationId"] = ["Observation id is required and cannot exceed 100 characters."];
            }
            else if (!ids.Add(observation.ObservationId.Trim()))
            {
                errors[$"observations[{index}].observationId"] = ["Observation ids must be unique."];
            }
            if (string.IsNullOrWhiteSpace(observation.RawPhrase) || observation.RawPhrase.Trim().Length > 300)
            {
                errors[$"observations[{index}].rawPhrase"] = ["Raw phrase is required and cannot exceed 300 characters."];
            }
            if (observation.NormalizedName?.Trim().Length > 300)
            {
                errors[$"observations[{index}].normalizedName"] = ["Normalized name cannot exceed 300 characters."];
            }
            ValidateConfidence(observation.IdentityConfidence, $"observations[{index}].identityConfidence", errors);
            if (observation.QuantityConfidence is { } quantityConfidence)
            {
                ValidateConfidence(quantityConfidence, $"observations[{index}].quantityConfidence", errors);
            }
            if (observation.EstimatedQuantity is < 0 or > MaximumQuantity)
            {
                errors[$"observations[{index}].estimatedQuantity"] = ["Estimated quantity must be between 0 and 99999999.999."];
            }
            if (observation.Evidence is { Count: > 20 })
            {
                errors[$"observations[{index}].evidence"] = ["At most 20 evidence references are allowed."];
            }
        }
        return errors;
    }

    public static Dictionary<string, string[]> ValidateReconciliation(
        PantryReconcileRequest request,
        string operationId,
        out ReconciliationMode mode,
        out StorageLocation? location)
    {
        var errors = new Dictionary<string, string[]>();
        mode = ParseMode(request.Mode, errors);
        location = ParseLocation(request.Scope?.Location, errors);
        var scope = request.Scope ?? new PantryReconciliationScope();
        var coverage = scope.Coverage?.Trim().ToLowerInvariant();

        if (operationId.Length == 0 || operationId.Length > 200)
        {
            errors["operationId"] = ["operationId is required and cannot exceed 200 characters."];
        }
        if (request.ExpectedPantryVersion is null or < 0)
        {
            errors["expectedPantryVersion"] = ["Read pantryVersion from resolve_observations and send it back unchanged."];
        }
        if (coverage is not ("partial" or "full"))
        {
            errors["scope.coverage"] = ["Coverage must be partial or full."];
        }
        else if (mode is not ReconciliationMode.Merge && coverage != "full")
        {
            errors["scope.coverage"] = ["Full coverage is required before decreases or removals are allowed."];
        }
        if (mode is not ReconciliationMode.Merge && string.IsNullOrWhiteSpace(scope.Location))
        {
            errors["scope.location"] = ["A location is required for a full-location reconciliation."];
        }
        if (mode == ReconciliationMode.ReplaceLocation
            && !(request.Confirm || request.ConfirmDestructiveChanges || request.Confirmed))
        {
            errors["confirmation"] = ["replace_location requires explicit confirmation of destructive changes."];
        }

        var items = request.Items ?? [];
        if (items.Count > MaximumObservations)
        {
            errors["items"] = [$"At most {MaximumObservations} resolved items are allowed."];
        }
        var observationIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            ValidateObservationId(item.ObservationId, $"items[{index}].observationId", observationIds, errors);
            if (!string.Equals(item.ItemType?.Trim(), IngredientType, StringComparison.OrdinalIgnoreCase))
            {
                errors[$"items[{index}].itemType"] = ["Only itemType 'ingredient' is supported by the pantry catalogue."];
            }
            ValidateConfidence(item.QuantityConfidence, $"items[{index}].quantityConfidence", errors);
            ValidateConfidence(item.IdentityConfidence, $"items[{index}].identityConfidence", errors);
            if (item.ConversionConfidence is { } conversionConfidence)
            {
                ValidateConfidence(conversionConfidence, $"items[{index}].conversionConfidence", errors);
            }
            if (item.ObservedQuantity is < 0 or > MaximumQuantity)
            {
                errors[$"items[{index}].observedQuantity"] = ["Observed quantity must be between 0 and 99999999.999."];
            }
            if (item.NormalizedQuantity is < 0 or > MaximumQuantity)
            {
                errors[$"items[{index}].normalizedQuantity"] = ["Normalized quantity must be between 0 and 99999999.999."];
            }
            if (item.ObservedUnit?.Trim().Length > 40)
            {
                errors[$"items[{index}].observedUnit"] = ["Observed unit cannot exceed 40 characters."];
            }
            if (item.NormalizedUnit?.Trim().Length > 10)
            {
                errors[$"items[{index}].normalizedUnit"] = ["Normalized unit cannot exceed 10 characters."];
            }
            if (item.PackageDescription?.Trim().Length > 200)
            {
                errors[$"items[{index}].packageDescription"] = ["Package description cannot exceed 200 characters."];
            }
        }

        var unmatched = request.Unmatched ?? [];
        var ambiguous = request.Ambiguous ?? [];
        if (unmatched.Count > MaximumObservations)
        {
            errors["unmatched"] = [$"At most {MaximumObservations} unmatched observations are allowed."];
        }
        if (ambiguous.Count > MaximumObservations)
        {
            errors["ambiguous"] = [$"At most {MaximumObservations} ambiguous observations are allowed."];
        }
        if (items.Count + unmatched.Count + ambiguous.Count > MaximumObservations)
        {
            errors["observations"] = [$"At most {MaximumObservations} observations are allowed in a reconciliation."];
        }
        for (var index = 0; index < unmatched.Count; index++)
        {
            var observation = unmatched[index];
            ValidateObservationId(observation.ObservationId, $"unmatched[{index}].observationId", observationIds, errors);
            ValidatePhrase(observation.RawPhrase, $"unmatched[{index}].rawPhrase", "Raw phrase", errors);
            ValidatePhrase(observation.NormalizedPhrase, $"unmatched[{index}].normalizedPhrase", "Normalized phrase", errors);
            ValidateConfidence(observation.IdentityConfidence, $"unmatched[{index}].identityConfidence", errors);
            if (observation.RecognitionConfidence is { } recognitionConfidence)
            {
                ValidateConfidence(recognitionConfidence, $"unmatched[{index}].recognitionConfidence", errors);
            }
        }
        for (var index = 0; index < ambiguous.Count; index++)
        {
            var observation = ambiguous[index];
            ValidateObservationId(observation.ObservationId, $"ambiguous[{index}].observationId", observationIds, errors);
            ValidatePhrase(observation.RawPhrase, $"ambiguous[{index}].rawPhrase", "Raw phrase", errors);
            ValidatePhrase(observation.NormalizedPhrase, $"ambiguous[{index}].normalizedPhrase", "Normalized phrase", errors);
            ValidateConfidence(observation.IdentityConfidence, $"ambiguous[{index}].identityConfidence", errors);
        }

        if (request.UnrecognizedCount is < 0 or > MaximumUnrecognizedCount)
        {
            errors["unrecognizedCount"] = [$"Unrecognized count must be between 0 and {MaximumUnrecognizedCount}."];
        }
        return errors;
    }

    private static void ValidateConfidence(
        decimal confidence,
        string field,
        IDictionary<string, string[]> errors)
    {
        if (confidence is < 0 or > 1)
        {
            errors[field] = ["Confidence must be between 0 and 1."];
        }
    }

    private static void ValidateObservationId(
        string? observationId,
        string field,
        ISet<string> observationIds,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(observationId) || observationId.Trim().Length > 100)
        {
            errors[field] = ["Observation id is required and cannot exceed 100 characters."];
        }
        else if (!observationIds.Add(observationId.Trim()))
        {
            errors[field] = ["Observation ids must be unique within a reconciliation."];
        }
    }

    private static void ValidatePhrase(
        string? phrase,
        string field,
        string label,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(phrase) || phrase.Trim().Length > 300)
        {
            errors[field] = [$"{label} is required and cannot exceed 300 characters."];
        }
    }

    private static ReconciliationMode ParseMode(
        string? value,
        IDictionary<string, string[]> errors)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (normalized == "merge")
        {
            return ReconciliationMode.Merge;
        }
        if (normalized == "reconcile_visible_snapshot")
        {
            return ReconciliationMode.ReconcileVisibleSnapshot;
        }
        if (normalized == "replace_location")
        {
            return ReconciliationMode.ReplaceLocation;
        }

        errors["mode"] = ["Mode must be merge, reconcile_visible_snapshot, or replace_location."];
        return ReconciliationMode.Merge;
    }

    private static StorageLocation? ParseLocation(
        string? value,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        var location = normalized switch
        {
            "refrigerator" or "fridge" => StorageLocation.Refrigerator,
            "freezer" => StorageLocation.Freezer,
            "cabinet" or "cupboard" => StorageLocation.Cabinet,
            _ => (StorageLocation?)null
        };
        if (location is null)
        {
            errors["scope.location"] = ["Location must be refrigerator, freezer, or cabinet."];
        }
        return location;
    }
}

internal enum ReconciliationMode
{
    Merge,
    ReconcileVisibleSnapshot,
    ReplaceLocation
}
