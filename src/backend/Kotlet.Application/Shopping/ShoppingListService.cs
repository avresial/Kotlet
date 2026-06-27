using Kotlet.Domain.Shopping;

namespace Kotlet.Application.Shopping;

public sealed class ShoppingListService(IShoppingListRepository repository)
{
    public async Task<IReadOnlyCollection<ShoppingListItemDto>> GetAllAsync(Guid houseId, CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(houseId, cancellationToken)).Select(ToDto).ToArray();

    public async Task<ShoppingListOperationResult> CreateAsync(Guid houseId, CreateShoppingListItemCommand command, CancellationToken cancellationToken)
    {
        if (!ValidQuantity(command.Quantity)) return InvalidQuantity();
        if (!await repository.IngredientExistsAsync(command.IngredientId, cancellationToken))
            return new(ShoppingListOperationStatus.NotFound);
        if (await repository.ItemExistsAsync(houseId, command.IngredientId, cancellationToken))
            return new(ShoppingListOperationStatus.Conflict, Message: "This ingredient is already on the shopping list.");

        var item = new ShoppingListItem { Id = Guid.NewGuid(), HouseId = houseId, IngredientId = command.IngredientId, Quantity = command.Quantity };
        repository.Add(item);
        await repository.SaveChangesAsync(cancellationToken);
        return new(ShoppingListOperationStatus.Success, ToDto((await repository.GetByIdAsync(item.Id, houseId, cancellationToken))!));
    }

    public async Task<ShoppingListOperationResult> UpdateAsync(Guid id, Guid houseId, UpdateShoppingListItemCommand command, CancellationToken cancellationToken)
    {
        if (!ValidQuantity(command.Quantity)) return InvalidQuantity();
        var item = await repository.GetByIdAsync(id, houseId, cancellationToken);
        if (item is null) return new(ShoppingListOperationStatus.NotFound);
        item.Quantity = command.Quantity;
        item.IsPurchased = command.IsPurchased;
        await repository.SaveChangesAsync(cancellationToken);
        return new(ShoppingListOperationStatus.Success, ToDto(item));
    }

    public async Task<ShoppingListOperationStatus> DeleteAsync(Guid id, Guid houseId, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, houseId, cancellationToken);
        if (item is null) return ShoppingListOperationStatus.NotFound;
        repository.Remove(item);
        await repository.SaveChangesAsync(cancellationToken);
        return ShoppingListOperationStatus.Success;
    }

    private static bool ValidQuantity(decimal quantity) => quantity > 0 && quantity <= 99999999.999m;
    private static ShoppingListOperationResult InvalidQuantity() => new(ShoppingListOperationStatus.ValidationFailed,
        ValidationErrors: new Dictionary<string, string[]> { ["quantity"] = ["Quantity must be greater than 0 and no more than 99999999.999."] });
    private static ShoppingListItemDto ToDto(ShoppingListItem item) => new(
        item.Id, item.IngredientId, item.Ingredient.Name, item.Ingredient.MeasurementUnit,
        item.Quantity, item.Ingredient.Price, decimal.Round(item.Quantity * item.Ingredient.Price, 2), item.IsPurchased);
}
