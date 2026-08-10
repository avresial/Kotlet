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
    private const string IngredientType = "ingredient";
    private const string UiResource = "ui://kotlet/data-v3";
    private const string DefaultLocale = "en";
    private const decimal MaximumQuantity = 99999999.999m;
    private const decimal MinimumReviewConfidence = 0.7m;
    private const decimal MinimumCandidateConfidence = 0.55m;
    private const decimal MinimumMatchedConfidence = 0.72m;
    private const decimal AmbiguousConfidenceMargin = 0.08m;
    private const decimal QuantityComparisonTolerance = 0.001m;
    private const int MaximumObservations = 200;
    private const int MaximumUnrecognizedCount = 1000;

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
        var validationErrors = ValidateResolutionRequest(observations, request.UnrecognizedCount);
        if (validationErrors.Count > 0)
        {
            return new("ValidationFailed", house.PantryVersion, [], [], [], request.UnrecognizedCount,
                ValidationErrors: validationErrors);
        }

        var catalog = await ingredientRepository.GetAllAsync(cancellationToken);
        var dictionary = await translations.GetAllAsync(cancellationToken);
        var searchableNames = BuildSearchableNames(catalog, dictionary);
        var matched = new List<PantryMatchedObservation>();
        var ambiguous = new List<PantryAmbiguousObservation>();
        var unmatched = new List<PantryUnmatchedObservation>();

        foreach (var observation in observations)
        {
            var normalizedPhrase = NormalizePhrase(observation.NormalizedName ?? observation.RawPhrase);
            var candidates = FindCandidates(normalizedPhrase, searchableNames);
            if (candidates.Count == 0 || candidates[0].Confidence < MinimumCandidateConfidence)
            {
                unmatched.Add(new(
                    observation.ObservationId.Trim(),
                    observation.RawPhrase.Trim(),
                    normalizedPhrase,
                    "No catalogue item was similar enough to resolve safely.",
                    observation.IdentityConfidence));
                continue;
            }

            var isAmbiguous = candidates[0].Confidence < MinimumMatchedConfidence
                || observation.IdentityConfidence < MinimumReviewConfidence
                || candidates.Count > 1
                && candidates[1].Confidence >= MinimumCandidateConfidence
                && candidates[0].Confidence - candidates[1].Confidence < AmbiguousConfidenceMargin;
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

        var validationErrors = ValidateReconciliationRequest(request, operationId, out var mode, out var location);
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

                    CapturePrevious(current, previousItems);
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
                        CapturePrevious(current, previousItems);
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

    public async Task<PantryUndoResponse> UndoAsync(
        Guid houseId,
        string undoToken,
        CancellationToken cancellationToken)
    {
        var token = undoToken?.Trim() ?? string.Empty;
        var house = await repository.GetHouseAsync(houseId, cancellationToken);
        if (house is null)
        {
            return new("NotFound", 0, token, Message: "Household not found.");
        }

        var existingOperation = token.Length == 0
            ? null
            : await repository.GetOperationByUndoTokenAsync(houseId, token, cancellationToken);
        if (existingOperation is null)
        {
            return new("NotFound", house.PantryVersion, token, Message: "Undo token was not found or has expired.");
        }
        if (existingOperation.UndoResponseJson is not null)
        {
            return DeserializeUndoResponse(existingOperation.UndoResponseJson);
        }
        if (existingOperation.UndoStateJson is null)
        {
            return new("ValidationFailed", house.PantryVersion, token,
                Message: "This reconciliation has no destructive changes to undo.");
        }

        try
        {
            return await repository.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var transactionHouse = await repository.GetHouseAsync(houseId, transactionCancellationToken);
                var operation = await repository.GetOperationByUndoTokenAsync(
                    houseId, token, transactionCancellationToken);
                if (transactionHouse is null || operation is null)
                {
                    return new PantryUndoResponse("NotFound", transactionHouse?.PantryVersion ?? 0, token,
                        Message: "Undo token was not found or has expired.");
                }
                if (operation.UndoResponseJson is not null)
                {
                    return DeserializeUndoResponse(operation.UndoResponseJson);
                }
                if (transactionHouse.PantryVersion != operation.PantryVersion)
                {
                    return new PantryUndoResponse(
                        "Conflict",
                        transactionHouse.PantryVersion,
                        token,
                        new Dictionary<string, string[]>
                        {
                            ["pantryVersion"] = ["The pantry changed after this scan; undo it only from the current state."]
                        });
                }

                var undoStateJson = operation.UndoStateJson;
                if (undoStateJson is null)
                {
                    return new PantryUndoResponse("ValidationFailed", transactionHouse.PantryVersion, token,
                        Message: "This reconciliation has no destructive changes to undo.");
                }
                var state = JsonSerializer.Deserialize<PantryUndoState>(undoStateJson, JsonOptions)
                    ?? throw new InvalidOperationException("The pantry undo state is invalid.");
                var currentItems = await repository.GetItemsAsync(houseId, transactionCancellationToken);
                var currentById = currentItems.ToDictionary(item => item.Id);
                var currentByIngredient = currentItems.ToDictionary(item => item.IngredientId);
                foreach (var addedItemId in state.AddedPantryItemIds)
                {
                    if (currentById.TryGetValue(addedItemId, out var addedItem))
                    {
                        repository.Remove(addedItem);
                    }
                }

                foreach (var previous in state.PreviousItems)
                {
                    if (currentById.TryGetValue(previous.PantryItemId, out var current))
                    {
                        Restore(current, previous);
                        continue;
                    }
                    if (currentByIngredient.ContainsKey(previous.IngredientId))
                    {
                        return new PantryUndoResponse(
                            "Conflict",
                            transactionHouse.PantryVersion,
                            token,
                            Message: "The pantry contains another item for a removed ingredient; undo was not applied.");
                    }

                    var ingredient = await ingredientRepository.GetByIdAsync(
                        previous.IngredientId, tracked: false, transactionCancellationToken);
                    if (ingredient is null)
                    {
                        return new PantryUndoResponse(
                            "Conflict",
                            transactionHouse.PantryVersion,
                            token,
                            Message: "An ingredient needed to restore the pantry item is no longer available.");
                    }

                    repository.Add(new PantryItem
                    {
                        Id = previous.PantryItemId,
                        HouseId = houseId,
                        IngredientId = previous.IngredientId,
                        Quantity = Quantity.FromAmount(previous.Quantity),
                        ExpirationDate = previous.ExpirationDate,
                        StorageLocation = previous.StorageLocation,
                        LastObservedQuantity = previous.LastObservedQuantity,
                        LastObservedUnit = previous.LastObservedUnit,
                        PackageDescription = previous.PackageDescription,
                        ConversionConfidence = previous.ConversionConfidence,
                        LastObservedAtUtc = previous.LastObservedAtUtc,
                        LastObservationIdsJson = previous.LastObservationIdsJson,
                        Ingredient = ingredient
                    });
                }

                transactionHouse.PantryVersion++;
                var response = new PantryUndoResponse("Success", transactionHouse.PantryVersion, token);
                operation.UndoneAtUtc = DateTimeOffset.UtcNow;
                operation.UndoResponseJson = JsonSerializer.Serialize(response, JsonOptions);
                return response;
            }, cancellationToken);
        }
        catch (PantryConcurrencyException)
        {
            var actualVersion = await repository.GetPantryVersionAsync(houseId, cancellationToken) ?? 0;
            return new PantryUndoResponse("Conflict", actualVersion, token,
                Message: "The pantry changed while undo was being applied.");
        }
    }

    private static Dictionary<string, string[]> ValidateResolutionRequest(
        IReadOnlyList<PantryObservation> observations,
        int unrecognizedCount)
    {
        var errors = new Dictionary<string, string[]>();
        if (observations.Count == 0 && unrecognizedCount == 0)
        {
            errors["observations"] = ["Provide at least one observation or a positive unrecognizedCount."];
        }
        if (observations.Count > MaximumObservations)
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

    private static Dictionary<string, string[]> ValidateReconciliationRequest(
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
        if (mode is not ReconciliationMode.Merge && string.IsNullOrWhiteSpace(scope.Location))
        {
            errors["scope.location"] = ["A location is required for a full-location reconciliation."];
        }
        if (mode is not ReconciliationMode.Merge && coverage != "full")
        {
            errors["scope.coverage"] = ["Full coverage is required before decreases or removals are allowed."];
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
            if (string.IsNullOrWhiteSpace(item.ObservationId) || item.ObservationId.Trim().Length > 100)
            {
                errors[$"items[{index}].observationId"] = ["Observation id is required and cannot exceed 100 characters."];
            }
            else if (!observationIds.Add(item.ObservationId.Trim()))
            {
                errors[$"items[{index}].observationId"] = ["Observation ids must be unique within a reconciliation."];
            }
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

        if (request.UnrecognizedCount is < 0 or > MaximumUnrecognizedCount)
        {
            errors["unrecognizedCount"] = [$"Unrecognized count must be between 0 and {MaximumUnrecognizedCount}."];
        }
        return errors;
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
            .GroupBy(item => NormalizePhrase(item.NormalizedPhrase), StringComparer.Ordinal)
            .Select(group => new MissedPhrase(
                group.First().RawPhrase,
                group.Key,
                group.SelectMany(item => item.CandidateIds).Distinct().ToArray(),
                group.Average(item => item.Confidence)))
            .ToList();

        foreach (var record in records)
        {
            var existing = await repository.GetUnmatchedPhraseAsync(
                houseId, record.NormalizedPhrase, locale, cancellationToken);
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
                    FirstSeenAtUtc = DateTimeOffset.UtcNow,
                    LastSeenAtUtc = DateTimeOffset.UtcNow,
                    OccurrenceCount = 1
                });
            }
            else
            {
                existing.RawPhrase = Limit(record.RawPhrase, 300);
                existing.CandidateIdsJson = JsonSerializer.Serialize(record.CandidateIds, JsonOptions);
                existing.RecognitionConfidence = record.Confidence;
                existing.LastSeenAtUtc = DateTimeOffset.UtcNow;
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

    private static bool ApplyObservation(
        PantryItem item,
        PreparedReconcileItem prepared,
        IReadOnlyList<string> observationIds,
        StorageLocation? location,
        DateTimeOffset observedAt)
    {
        var changed = false;
        if (location.HasValue && item.StorageLocation != location)
        {
            item.StorageLocation = location;
            changed = true;
        }

        var observedUnit = NormalizeUnit(prepared.Item.ObservedUnit);
        var packageDescription = NormalizeDescription(prepared.Item.PackageDescription);
        var observationIdsJson = JsonSerializer.Serialize(observationIds, JsonOptions);
        if (item.LastObservedQuantity != prepared.Item.ObservedQuantity
            || !string.Equals(item.LastObservedUnit, observedUnit, StringComparison.Ordinal)
            || !string.Equals(item.PackageDescription, packageDescription, StringComparison.Ordinal)
            || item.ConversionConfidence != prepared.ConversionConfidence
            || item.LastObservationIdsJson != observationIdsJson)
        {
            changed = true;
        }

        item.LastObservedQuantity = prepared.Item.ObservedQuantity;
        item.LastObservedUnit = observedUnit;
        item.PackageDescription = packageDescription;
        item.ConversionConfidence = prepared.ConversionConfidence;
        item.LastObservedAtUtc = observedAt;
        item.LastObservationIdsJson = observationIdsJson;
        return changed;
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

    private static void CapturePrevious(PantryItem item, ICollection<PantryUndoItem> previousItems)
    {
        if (previousItems.Any(previous => previous.PantryItemId == item.Id))
        {
            return;
        }

        previousItems.Add(new(
            item.Id,
            item.IngredientId,
            item.Quantity.Amount,
            item.ExpirationDate,
            item.StorageLocation,
            item.LastObservedQuantity,
            item.LastObservedUnit,
            item.PackageDescription,
            item.ConversionConfidence,
            item.LastObservedAtUtc,
            item.LastObservationIdsJson));
    }

    private static void Restore(PantryItem item, PantryUndoItem previous)
    {
        item.Quantity = Quantity.FromAmount(previous.Quantity);
        item.ExpirationDate = previous.ExpirationDate;
        item.StorageLocation = previous.StorageLocation;
        item.LastObservedQuantity = previous.LastObservedQuantity;
        item.LastObservedUnit = previous.LastObservedUnit;
        item.PackageDescription = previous.PackageDescription;
        item.ConversionConfidence = previous.ConversionConfidence;
        item.LastObservedAtUtc = previous.LastObservedAtUtc;
        item.LastObservationIdsJson = previous.LastObservationIdsJson;
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

    private static List<CatalogName> BuildSearchableNames(
        IReadOnlyCollection<Ingredient> ingredients,
        IReadOnlyDictionary<string, string> translations)
    {
        var names = new List<CatalogName>();
        foreach (var ingredient in ingredients)
        {
            AddCatalogName(names, ingredient.Id, ingredient.Name, ingredient.MeasurementUnit);
            foreach (var language in TranslationKeys.TranslatedLanguages)
            {
                var translation = translations.GetValueOrDefault(TranslationKeys.Ingredient(ingredient.Id, language));
                if (!string.IsNullOrWhiteSpace(translation))
                {
                    AddCatalogName(names, ingredient.Id, translation, ingredient.MeasurementUnit);
                }
            }
        }
        return names;
    }

    private static void AddCatalogName(
        ICollection<CatalogName> names,
        Guid ingredientId,
        string name,
        string measurementUnit) =>
        names.Add(new(ingredientId, name.Trim(), measurementUnit, NormalizePhrase(name)));

    private static List<ScoredCandidate> FindCandidates(
        string normalizedPhrase,
        IReadOnlyCollection<CatalogName> searchableNames) =>
        searchableNames
            .Where(candidate => normalizedPhrase.Length > 0 && candidate.NormalizedName.Length > 0)
            .Select(candidate => new ScoredCandidate(
                candidate.Id,
                candidate.Name,
                candidate.MeasurementUnit,
                Similarity(normalizedPhrase, candidate.NormalizedName)))
            .GroupBy(candidate => candidate.Id)
            .Select(group => group.OrderByDescending(candidate => candidate.Confidence)
                .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Id)
            .ToList();

    private static decimal Similarity(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 1m;
        }

        var distance = Distance(left, right);
        var denominator = Math.Max(left.Length, right.Length);
        return denominator == 0 ? 0 : Math.Round(1m - (decimal)distance / denominator, 3);
    }

    private static int Distance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            }
            previous = current;
        }
        return previous[right.Length];
    }

    private static string NormalizePhrase(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return string.Empty;
        }

        var characters = phrase.Trim().ToUpperInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();
        return string.Join(' ', new string(characters)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
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

    private static string Limit(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()[..Math.Min(maximum, value.Trim().Length)];

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

    private static PantryReconciliationResponse DeserializeResponse(string json) =>
        JsonSerializer.Deserialize<PantryReconciliationResponse>(json, JsonOptions)
        ?? throw new InvalidOperationException("The stored pantry reconciliation is invalid.");

    private static PantryUndoResponse DeserializeUndoResponse(string json) =>
        JsonSerializer.Deserialize<PantryUndoResponse>(json, JsonOptions)
        ?? throw new InvalidOperationException("The stored pantry undo result is invalid.");

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

    private enum ReconciliationMode
    {
        Merge,
        ReconcileVisibleSnapshot,
        ReplaceLocation
    }

    private sealed record CatalogName(Guid Id, string Name, string MeasurementUnit, string NormalizedName);

    private sealed record ScoredCandidate(Guid Id, string Name, string MeasurementUnit, decimal Confidence);

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
