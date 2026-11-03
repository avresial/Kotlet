using System.Text.Json;
using Kotlet.Application.Ingredients;
using Kotlet.Application.Measurements;
using Kotlet.Application.Translations;
using Kotlet.Domain.Common;
using Kotlet.Domain.Houses;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.Pantry;

namespace Kotlet.Application.Pantry;

public sealed class PantryReconciliationService(
    IPantryReconciliationRepository repository,
    IIngredientRepository ingredientRepository,
    ITranslationRepository translations,
    MeasurementMappingService measurements)
{
    private const string IngredientType = PantryReconciliationRequestValidator.IngredientType;
    private const string UiResource = "ui://kotlet/data-v3";
    private const string DefaultLocale = "en";
    private const decimal MinimumReviewConfidence = 0.7m;
    private const decimal QuantityComparisonTolerance = 0.001m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PantryObservationResolutionResponse> ResolveAsync(
        Guid houseId,
        PantryResolveObservationsRequest request,
        CancellationToken cancellationToken)
    {
        var house = await repository.GetHouseAsync(houseId, cancellationToken);
        if (house is null)
        {
            return new("NotFound", 0, [], [], [], 0, Message: "Household not found.");
        }

        var observations = request.Observations ?? [];
        var validationErrors = PantryReconciliationRequestValidator.ValidateResolution(observations, request.UnrecognizedCount);
        if (validationErrors.Count > 0)
        {
            return new("ValidationFailed", house.PantryVersion, [], [], [], request.UnrecognizedCount,
                ValidationErrors: validationErrors);
        }

        var catalog = await ingredientRepository.GetAllAsync(cancellationToken);
        var dictionary = await translations.GetAllAsync(cancellationToken);
        var matcher = new IngredientPhraseMatcher(catalog, dictionary);
        var matched = new List<PantryMatchedObservation>();
        var ambiguous = new List<PantryAmbiguousObservation>();
        var unmatched = new List<PantryUnmatchedObservation>();

        foreach (var observation in observations)
        {
            var normalizedPhrase = IngredientPhraseMatcher.NormalizePhrase(observation.NormalizedName ?? observation.RawPhrase);
            var match = matcher.Match(normalizedPhrase);
            var candidates = match.Candidates;
            if (match.Kind == PhraseMatchKind.Unmatched)
            {
                unmatched.Add(new(
                    observation.ObservationId.Trim(),
                    observation.RawPhrase.Trim(),
                    normalizedPhrase,
                    "No catalogue item was similar enough to resolve safely.",
                    observation.IdentityConfidence));
                continue;
            }

            var isAmbiguous = match.Kind == PhraseMatchKind.Ambiguous
                || observation.IdentityConfidence < MinimumReviewConfidence;
            if (isAmbiguous)
            {
                ambiguous.Add(new(
                    observation.ObservationId.Trim(),
                    observation.RawPhrase.Trim(),
                    normalizedPhrase,
                    candidates.Take(5)
                        .Select(candidate => new PantryResolutionCandidate(
                            candidate.Id,
                            IngredientType,
                            candidate.Name,
                            candidate.MeasurementUnit,
                            candidate.Confidence))
                        .ToList(),
                    observation.IdentityConfidence));
                continue;
            }

            var best = candidates[0];
            matched.Add(new(
                observation.ObservationId.Trim(),
                observation.RawPhrase.Trim(),
                best.Id,
                IngredientType,
                best.Name,
                best.MeasurementUnit,
                best.Confidence == 1m ? "exact" : "similar",
                best.Confidence,
                observation.IdentityConfidence));
        }

        return new("Success", house.PantryVersion, matched, ambiguous, unmatched, request.UnrecognizedCount);
    }

    public async Task<PantryReconciliationResponse> ReconcileAsync(
        Guid houseId,
        PantryReconcileRequest request,
        CancellationToken cancellationToken)
    {
        var house = await repository.GetHouseAsync(houseId, cancellationToken);
        if (house is null)
        {
            return NotFoundResponse();
        }

        var operationId = request.OperationId?.Trim() ?? string.Empty;
        var existingOperation = operationId.Length == 0
            ? null
            : await repository.GetOperationAsync(houseId, operationId, cancellationToken);
        if (existingOperation is not null)
        {
            return DeserializeResponse(existingOperation.ResponseJson);
        }

        var validationErrors = PantryReconciliationRequestValidator.ValidateReconciliation(request, operationId, out var mode, out var location);
        var itemInputs = request.Items ?? [];
        var ingredients = await ingredientRepository.GetByIdsAsync(
            itemInputs.Select(item => item.ItemId).Where(id => id != Guid.Empty).Distinct().ToArray(),
            cancellationToken);
        var preparedItems = new List<PreparedReconcileItem>(itemInputs.Count);
        for (var index = 0; index < itemInputs.Count; index++)
        {
            var input = itemInputs[index];
            if (!ingredients.TryGetValue(input.ItemId, out var ingredient))
            {
                validationErrors[$"items[{index}].itemId"] = ["The catalogue item was not found."];
                continue;
            }

            var prepared = PrepareItem(input, ingredient, validationErrors, index);
            if (prepared is not null)
            {
                preparedItems.Add(prepared);
            }
        }

        if (validationErrors.Count > 0)
        {
            return ValidationResponse(house.PantryVersion, validationErrors);
        }

        try
        {
            return await repository.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var transactionHouse = await repository.GetHouseAsync(houseId, transactionCancellationToken);
                if (transactionHouse is null)
                {
                    return NotFoundResponse();
                }

                var transactionOperation = await repository.GetOperationAsync(
                    houseId, operationId, transactionCancellationToken);
                if (transactionOperation is not null)
                {
                    return DeserializeResponse(transactionOperation.ResponseJson);
                }

                if (transactionHouse.PantryVersion != request.ExpectedPantryVersion)
                {
                    return ConflictResponse(
                        transactionHouse.PantryVersion,
                        $"The pantry has changed. Read pantryVersion {transactionHouse.PantryVersion} and retry.");
                }

                var currentItems = await repository.GetItemsAsync(houseId, transactionCancellationToken);
                var existingByIngredient = currentItems.ToDictionary(item => item.IngredientId);
                var consolidated = Consolidate(preparedItems);
                var observedIngredientIds = consolidated.Select(item => item.Prepared.Item.ItemId).ToHashSet();
                var unresolvedCandidateIds = (request.Unmatched ?? [])
                    .SelectMany(item => item.CandidateIds ?? [])
                    .Concat((request.Ambiguous ?? []).SelectMany(item =>
                        item.Candidates.Select(candidate => candidate.ItemId)));
                observedIngredientIds.UnionWith(unresolvedCandidateIds);
                var added = new List<PantryDiffEntry>();
                var increased = new List<PantryDiffEntry>();
                var decreased = new List<PantryDiffEntry>();
                var removed = new List<PantryDiffEntry>();
                var unchanged = new List<PantryDiffEntry>();
                var needsReview = new List<PantryReviewEntry>();
                var previousItems = new List<PantryUndoItem>();
                var addedPantryItemIds = new List<Guid>();
                var now = DateTimeOffset.UtcNow;

                foreach (var groupedItem in consolidated.OrderBy(item => item.Prepared.Ingredient.Name))
                {
                    var prepared = groupedItem.Prepared;
                    if (prepared.NeedsReview || prepared.SafeQuantity is null)
                    {
                        needsReview.Add(ToReviewEntry(prepared));
                        continue;
                    }

                    var quantity = prepared.SafeQuantity.Value;
                    if (!existingByIngredient.TryGetValue(prepared.Item.ItemId, out var current))
                    {
                        var item = new PantryItem
                        {
                            Id = Guid.NewGuid(),
                            HouseId = houseId,
                            IngredientId = prepared.Item.ItemId,
                            Quantity = Quantity.FromAmount(quantity),
                            StorageLocation = location,
                            Ingredient = prepared.Ingredient,
                            LastObservedQuantity = prepared.Item.ObservedQuantity,
                            LastObservedUnit = NormalizeUnit(prepared.Item.ObservedUnit),
                            PackageDescription = NormalizeDescription(prepared.Item.PackageDescription),
                            ConversionConfidence = prepared.ConversionConfidence,
                            LastObservedAtUtc = now,
                            LastObservationIdsJson = JsonSerializer.Serialize(groupedItem.ObservationIds, JsonOptions)
                        };
                        repository.Add(item);
                        existingByIngredient[prepared.Item.ItemId] = item;
                        addedPantryItemIds.Add(item.Id);
                        added.Add(ToDiffEntry(
                            item,
                            null,
                            quantity,
                            groupedItem.ObservationIds,
                            prepared));
                        continue;
                    }

                    var previousQuantity = current.Quantity.Amount;
                    var metadataChanged = ObservationChanges(current, prepared, groupedItem.ObservationIds, location);
                    var quantityChanged = previousQuantity != quantity;
                    if (!quantityChanged && !metadataChanged)
                    {
                        unchanged.Add(ToDiffEntry(
                            current,
                            previousQuantity,
                            quantity,
                            groupedItem.ObservationIds,
                            prepared));
                        continue;
                    }

                    PantryUndoService.CapturePrevious(current, previousItems);
                    ApplyObservation(current, prepared, groupedItem.ObservationIds, location, now);
                    current.Quantity = Quantity.FromAmount(quantity);
                    var diff = ToDiffEntry(
                        current,
                        previousQuantity,
                        quantity,
                        groupedItem.ObservationIds,
                        prepared);
                    if (!quantityChanged)
                    {
                        unchanged.Add(diff);
                    }
                    else if (quantity > previousQuantity)
                    {
                        increased.Add(diff);
                    }
                    else
                    {
                        decreased.Add(diff);
                    }
                }

                if (mode is not ReconciliationMode.Merge)
                {
                    foreach (var current in currentItems
                                 .Where(item => item.StorageLocation == location)
                                 .Where(item => !observedIngredientIds.Contains(item.IngredientId))
                                 .ToList())
                    {
                        PantryUndoService.CapturePrevious(current, previousItems);
                        removed.Add(ToDiffEntry(
                            current,
                            current.Quantity.Amount,
                            null,
                            [],
                            null));
                        repository.Remove(current);
                    }
                }

                await StoreMissedPhrasesAsync(
                    houseId,
                    request.Unmatched ?? [],
                    request.Ambiguous ?? [],
                    request.Locale,
                    transactionCancellationToken);

                transactionHouse.PantryVersion++;
                var undoToken = previousItems.Count > 0 || addedPantryItemIds.Count > 0
                    ? Guid.NewGuid().ToString("N")
                    : null;
                var response = new PantryReconciliationResponse(
                    "Success",
                    transactionHouse.PantryVersion,
                    added,
                    increased,
                    decreased,
                    removed,
                    unchanged,
                    needsReview,
                    request.Unmatched ?? [],
                    request.Ambiguous ?? [],
                    request.UnrecognizedCount,
                    undoToken,
                    UiResource);
                var operation = new PantryReconciliationOperation
                {
                    Id = Guid.NewGuid(),
                    HouseId = houseId,
                    OperationId = operationId,
                    PantryVersion = transactionHouse.PantryVersion,
                    ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
                    UndoToken = undoToken,
                    UndoStateJson = undoToken is null
                        ? null
                        : JsonSerializer.Serialize(new PantryUndoState(previousItems, addedPantryItemIds), JsonOptions),
                    CreatedAtUtc = now
                };
                repository.AddOperation(operation);
                return response;
            }, cancellationToken);
        }
        catch (PantryConcurrencyException)
        {
            var actualVersion = await repository.GetPantryVersionAsync(houseId, cancellationToken) ?? 0;
            return ConflictResponse(actualVersion, "The pantry changed while this scan was being applied. Retry with the latest pantryVersion.");
        }
    }

    private PreparedReconcileItem? PrepareItem(
        PantryReconcileItem item,
        Ingredient ingredient,
        IDictionary<string, string[]> errors,
        int index)
    {
        var observedUnit = NormalizeUnit(item.ObservedUnit);
        var normalizedUnit = NormalizeUnit(item.NormalizedUnit);
        var safeQuantity = (decimal?)null;
        var conversionConfidence = item.ConversionConfidence;
        var reviewReason = (string?)null;

        if (item.NormalizedQuantity is { } normalizedQuantity)
        {
            if (!string.Equals(normalizedUnit, ingredient.MeasurementUnit, StringComparison.Ordinal))
            {
                errors[$"items[{index}].normalizedUnit"] = [$"Normalized unit must be the ingredient base unit '{ingredient.MeasurementUnit}'."];
            }
            else if (item.ObservedQuantity is { } observedQuantity)
            {
                if (string.IsNullOrWhiteSpace(observedUnit))
                {
                    errors[$"items[{index}].observedUnit"] = ["Observed unit is required when observed quantity is supplied."];
                }
                else if (TryNormalize(observedQuantity, observedUnit, ingredient, out var convertedQuantity))
                {
                    if (Math.Abs(convertedQuantity - normalizedQuantity) > QuantityComparisonTolerance)
                    {
                        errors[$"items[{index}].normalizedQuantity"] = ["Normalized quantity does not match the backend's safe conversion."];
                    }
                    else
                    {
                        safeQuantity = normalizedQuantity;
                        conversionConfidence ??= string.Equals(observedUnit, ingredient.MeasurementUnit, StringComparison.Ordinal)
                            ? 1m
                            : item.QuantityConfidence;
                    }
                }
                else
                {
                    errors[$"items[{index}].observedUnit"] = ["The supplied observed unit cannot be safely converted for this ingredient."];
                }
            }
            else
            {
                safeQuantity = normalizedQuantity;
                conversionConfidence ??= 1m;
            }
        }
        else if (item.ObservedQuantity is { } observedQuantity)
        {
            if (string.IsNullOrWhiteSpace(observedUnit))
            {
                reviewReason = "Observed quantity has no unit.";
            }
            else if (TryNormalize(observedQuantity, observedUnit, ingredient, out var convertedQuantity))
            {
                safeQuantity = convertedQuantity;
                conversionConfidence ??= string.Equals(observedUnit, ingredient.MeasurementUnit, StringComparison.Ordinal)
                    ? 1m
                    : item.QuantityConfidence;
            }
            else if (item.QuantityConfidence < MinimumReviewConfidence)
            {
                reviewReason = "The quantity unit needs review before it can be converted safely.";
            }
            else
            {
                errors[$"items[{index}].observedUnit"] = ["The supplied observed unit cannot be safely converted for this ingredient."];
            }
        }
        else
        {
            reviewReason = "No safe quantity was supplied.";
        }

        if (reviewReason is null && item.IdentityConfidence < MinimumReviewConfidence)
        {
            reviewReason = "Identity confidence is too low to change the pantry automatically.";
        }
        else if (reviewReason is null && item.QuantityConfidence < MinimumReviewConfidence)
        {
            reviewReason = "Quantity confidence is too low to change the pantry automatically.";
        }

        return new(item, ingredient, safeQuantity, conversionConfidence, reviewReason);
    }

    private async Task StoreMissedPhrasesAsync(
        Guid houseId,
        IReadOnlyList<PantryUnmatchedObservation> unmatched,
        IReadOnlyList<PantryAmbiguousObservation> ambiguous,
        string? requestedLocale,
        CancellationToken cancellationToken)
    {
        var locale = NormalizeLocale(requestedLocale);
        var records = unmatched
            .Select(item => new MissedPhrase(
                item.RawPhrase,
                item.NormalizedPhrase,
                item.CandidateIds ?? [],
                item.RecognitionConfidence ?? item.IdentityConfidence))
            .Concat(ambiguous.Select(item => new MissedPhrase(
                item.RawPhrase,
                item.NormalizedPhrase,
                item.Candidates.Select(candidate => candidate.ItemId).ToArray(),
                item.IdentityConfidence)))
            .Where(item => !string.IsNullOrWhiteSpace(item.NormalizedPhrase))
            .GroupBy(item => Limit(IngredientPhraseMatcher.NormalizePhrase(item.NormalizedPhrase), 300), StringComparer.Ordinal)
            .Select(group => new MissedPhrase(
                group.First().RawPhrase,
                group.Key,
                group.SelectMany(item => item.CandidateIds).Distinct().ToArray(),
                group.Average(item => item.Confidence)))
            .ToList();

        var existingPhrases = await repository.GetUnmatchedPhrasesAsync(
            houseId,
            records.Select(record => record.NormalizedPhrase).ToArray(),
            locale,
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var record in records)
        {
            var existing = existingPhrases.GetValueOrDefault(record.NormalizedPhrase);
            if (existing is null)
            {
                repository.AddUnmatchedPhrase(new PantryUnmatchedPhrase
                {
                    Id = Guid.NewGuid(),
                    HouseId = houseId,
                    RawPhrase = Limit(record.RawPhrase, 300),
                    NormalizedPhrase = Limit(record.NormalizedPhrase, 300),
                    Locale = locale,
                    CandidateIdsJson = JsonSerializer.Serialize(record.CandidateIds, JsonOptions),
                    RecognitionConfidence = record.Confidence,
                    FirstSeenAtUtc = now,
                    LastSeenAtUtc = now,
                    OccurrenceCount = 1
                });
            }
            else
            {
                existing.RawPhrase = Limit(record.RawPhrase, 300);
                existing.CandidateIdsJson = JsonSerializer.Serialize(record.CandidateIds, JsonOptions);
                existing.RecognitionConfidence = record.Confidence;
                existing.LastSeenAtUtc = now;
                existing.OccurrenceCount++;
            }
        }
    }

    private static List<ConsolidatedReconcileItem> Consolidate(
        IReadOnlyList<PreparedReconcileItem> preparedItems) =>
        preparedItems
            .GroupBy(item => item.Item.ItemId)
            .Select(group =>
            {
                var selected = group
                    .Where(item => !item.NeedsReview && item.SafeQuantity.HasValue)
                    .OrderByDescending(item => item.SafeQuantity)
                    .ThenByDescending(item => item.Item.QuantityConfidence)
                    .FirstOrDefault()
                    ?? group.OrderByDescending(item => item.Item.QuantityConfidence).First();
                return new ConsolidatedReconcileItem(
                    selected,
                    group.Select(item => item.Item.ObservationId.Trim()).Distinct(StringComparer.Ordinal).ToList());
            })
            .ToList();

    private static void ApplyObservation(
        PantryItem item,
        PreparedReconcileItem prepared,
        IReadOnlyList<string> observationIds,
        StorageLocation? location,
        DateTimeOffset observedAt)
    {
        if (location.HasValue && item.StorageLocation != location)
        {
            item.StorageLocation = location;
        }

        var observedUnit = NormalizeUnit(prepared.Item.ObservedUnit);
        var packageDescription = NormalizeDescription(prepared.Item.PackageDescription);
        var observationIdsJson = JsonSerializer.Serialize(observationIds, JsonOptions);
        item.LastObservedQuantity = prepared.Item.ObservedQuantity;
        item.LastObservedUnit = observedUnit;
        item.PackageDescription = packageDescription;
        item.ConversionConfidence = prepared.ConversionConfidence;
        item.LastObservedAtUtc = observedAt;
        item.LastObservationIdsJson = observationIdsJson;
    }

    private static bool ObservationChanges(
        PantryItem item,
        PreparedReconcileItem prepared,
        IReadOnlyList<string> observationIds,
        StorageLocation? location)
    {
        var observedUnit = NormalizeUnit(prepared.Item.ObservedUnit);
        var packageDescription = NormalizeDescription(prepared.Item.PackageDescription);
        var observationIdsJson = JsonSerializer.Serialize(observationIds, JsonOptions);
        return (location.HasValue && item.StorageLocation != location)
            || item.LastObservedQuantity != prepared.Item.ObservedQuantity
            || !string.Equals(item.LastObservedUnit, observedUnit, StringComparison.Ordinal)
            || !string.Equals(item.PackageDescription, packageDescription, StringComparison.Ordinal)
            || item.ConversionConfidence != prepared.ConversionConfidence
            || item.LastObservationIdsJson != observationIdsJson;
    }

    private static PantryDiffEntry ToDiffEntry(
        PantryItem item,
        decimal? previousQuantity,
        decimal? newQuantity,
        IReadOnlyList<string> observationIds,
        PreparedReconcileItem? prepared)
    {
        var change = previousQuantity.HasValue && newQuantity.HasValue
            ? newQuantity.Value - previousQuantity.Value
            : newQuantity ?? (previousQuantity.HasValue ? -previousQuantity.Value : null);
        return new(
            item.IngredientId,
            IngredientType,
            item.Id,
            item.Ingredient.Name,
            item.Ingredient.MeasurementUnit,
            previousQuantity,
            previousQuantity.HasValue ? item.Ingredient.MeasurementUnit : null,
            newQuantity,
            newQuantity.HasValue ? item.Ingredient.MeasurementUnit : null,
            change,
            observationIds,
            prepared?.Item.ObservedQuantity,
            NormalizeUnit(prepared?.Item.ObservedUnit),
            prepared?.ConversionConfidence,
            NormalizeDescription(prepared?.Item.PackageDescription));
    }

    private static PantryReviewEntry ToReviewEntry(PreparedReconcileItem prepared) => new(
        prepared.Item.ObservationId.Trim(),
        prepared.Item.ItemId,
        prepared.Ingredient.Name,
        prepared.ReviewReason ?? "The observation needs review before it can change the pantry.",
        prepared.Item.ObservedQuantity,
        NormalizeUnit(prepared.Item.ObservedUnit),
        prepared.Item.NormalizedQuantity,
        NormalizeUnit(prepared.Item.NormalizedUnit),
        prepared.Item.QuantityConfidence,
        prepared.Item.IdentityConfidence,
        NormalizeDescription(prepared.Item.PackageDescription));

    private bool TryNormalize(
        decimal quantity,
        string unit,
        Ingredient ingredient,
        out decimal normalizedQuantity)
    {
        if (quantity == 0 && string.Equals(unit, ingredient.MeasurementUnit, StringComparison.Ordinal))
        {
            normalizedQuantity = 0;
            return true;
        }

        var normalized = measurements.Normalize(quantity, unit, ingredient);
        normalizedQuantity = normalized?.Quantity ?? 0;
        return normalized is not null;
    }

    private static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return DefaultLocale;
        }

        var normalized = locale.Trim().Replace('_', '-').ToLowerInvariant();
        return normalized[..Math.Min(20, normalized.Length)];
    }

    private static string? NormalizeUnit(string? unit) =>
        string.IsNullOrWhiteSpace(unit) ? null : unit.Trim().ToLowerInvariant();

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : Limit(description.Trim(), 200);

    private static string Limit(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed[..Math.Min(maximum, trimmed.Length)];
    }

    private static PantryReconciliationResponse DeserializeResponse(string json) =>
        JsonSerializer.Deserialize<PantryReconciliationResponse>(json, JsonOptions)
        ?? throw new InvalidOperationException("The stored pantry reconciliation is invalid.");

    private static PantryReconciliationResponse NotFoundResponse() =>
        EmptyResponse("NotFound", 0, "Household not found.");

    private static PantryReconciliationResponse ValidationResponse(
        long pantryVersion,
        IReadOnlyDictionary<string, string[]> errors) =>
        EmptyResponse("ValidationFailed", pantryVersion, validationErrors: errors);

    private static PantryReconciliationResponse ConflictResponse(long pantryVersion, string message) =>
        EmptyResponse("Conflict", pantryVersion, message: message);

    private static PantryReconciliationResponse EmptyResponse(
        string status,
        long pantryVersion,
        string? message = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null) =>
        new(status, pantryVersion, [], [], [], [], [], [], [], [], 0, null, UiResource, validationErrors, message);

    private sealed record PreparedReconcileItem(
        PantryReconcileItem Item,
        Ingredient Ingredient,
        decimal? SafeQuantity,
        decimal? ConversionConfidence,
        string? ReviewReason)
    {
        public bool NeedsReview => ReviewReason is not null;
    }

    private sealed record ConsolidatedReconcileItem(
        PreparedReconcileItem Prepared,
        IReadOnlyList<string> ObservationIds);

    private sealed record MissedPhrase(
        string RawPhrase,
        string NormalizedPhrase,
        IReadOnlyList<Guid> CandidateIds,
        decimal Confidence);
}
