using System.Text.Json;
using Kotlet.Application.Ingredients;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.Pantry;

namespace Kotlet.Application.Pantry;

/// <summary>Reverts a stored pantry reconciliation when the pantry has not changed since.</summary>
public sealed class PantryUndoService(
    IPantryReconciliationRepository repository,
    IIngredientRepository ingredientRepository)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
                var addedItemIds = state.AddedPantryItemIds.ToHashSet();
                var ingredientsToRestore = new Dictionary<Guid, Ingredient>();
                foreach (var previous in state.PreviousItems)
                {
                    if (currentById.ContainsKey(previous.PantryItemId))
                    {
                        continue;
                    }
                    if (currentByIngredient.TryGetValue(previous.IngredientId, out var conflictingItem)
                        && !addedItemIds.Contains(conflictingItem.Id))
                    {
                        return new PantryUndoResponse(
                            "Conflict",
                            transactionHouse.PantryVersion,
                            token,
                            Message: "The pantry contains another item for a removed ingredient; undo was not applied.");
                    }

                    if (ingredientsToRestore.ContainsKey(previous.IngredientId))
                    {
                        continue;
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

                    ingredientsToRestore[previous.IngredientId] = ingredient;
                }

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
                        Ingredient = ingredientsToRestore[previous.IngredientId]
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

    /// <summary>Snapshots an item before a reconciliation changes or removes it, once per item.</summary>
    internal static void CapturePrevious(PantryItem item, ICollection<PantryUndoItem> previousItems)
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

    private static PantryUndoResponse DeserializeUndoResponse(string json) =>
        JsonSerializer.Deserialize<PantryUndoResponse>(json, JsonOptions)
        ?? throw new InvalidOperationException("The stored pantry undo result is invalid.");
}
