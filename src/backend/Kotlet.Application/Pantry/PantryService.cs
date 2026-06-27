using Kotlet.Domain.Pantry;

namespace Kotlet.Application.Pantry;

public sealed class PantryService(IPantryRepository repository)
{
    public async Task<IReadOnlyCollection<PantryItemDto>> GetAllAsync(Guid houseId, CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(houseId, cancellationToken)).Select(ToDto).ToArray();

    public async Task<PantryOperationResult> CreateAsync(Guid houseId, SavePantryItemCommand command, CancellationToken cancellationToken)
    {
        if (command.Quantity < 0 || command.Quantity > 99999999.999m)
            return InvalidQuantity();
        if (!await repository.IngredientExistsAsync(command.IngredientId, cancellationToken))
            return new(PantryOperationStatus.NotFound);
        if (await repository.ItemExistsAsync(houseId, command.IngredientId, cancellationToken))
            return new(PantryOperationStatus.Conflict, Message: "This ingredient is already in your pantry.");

        var item = new PantryItem { Id = Guid.NewGuid(), HouseId = houseId, IngredientId = command.IngredientId, Quantity = command.Quantity };
        repository.Add(item);
        await repository.SaveChangesAsync(cancellationToken);
        var saved = await repository.GetByIdAsync(item.Id, houseId, cancellationToken);
        return new(PantryOperationStatus.Success, ToDto(saved!));
    }

    public async Task<PantryOperationResult> UpdateAsync(Guid id, Guid houseId, decimal quantity, CancellationToken cancellationToken)
    {
        if (quantity < 0 || quantity > 99999999.999m)
            return InvalidQuantity();
        var item = await repository.GetByIdAsync(id, houseId, cancellationToken);
        if (item is null) return new(PantryOperationStatus.NotFound);
        item.Quantity = quantity;
        await repository.SaveChangesAsync(cancellationToken);
        return new(PantryOperationStatus.Success, ToDto(item));
    }

    public async Task<PantryOperationStatus> DeleteAsync(Guid id, Guid houseId, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, houseId, cancellationToken);
        if (item is null) return PantryOperationStatus.NotFound;
        repository.Remove(item);
        await repository.SaveChangesAsync(cancellationToken);
        return PantryOperationStatus.Success;
    }

    private static PantryOperationResult InvalidQuantity() => new(PantryOperationStatus.ValidationFailed,
        ValidationErrors: new Dictionary<string, string[]> { ["quantity"] = ["Quantity must be between 0 and 99999999.999."] });
    private static PantryItemDto ToDto(PantryItem item) =>
        new(item.Id, item.IngredientId, item.Ingredient.Name, item.Ingredient.MeasurementUnit, item.Quantity);
}
