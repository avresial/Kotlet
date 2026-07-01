using Kotlet.Domain.Common;
using Kotlet.Domain.Shopping;
using Kotlet.Application.Translations;

namespace Kotlet.Application.Shopping;

public sealed class ShoppingListService(IShoppingListRepository repository, ITranslationRepository translations)
{
    public async Task<IReadOnlyCollection<ShoppingListItemDto>> GetAllAsync(Guid houseId, string languageCode, CancellationToken cancellationToken)
    {
        var items = await repository.GetAllAsync(houseId, cancellationToken);
        var dictionary = await LoadTranslationsAsync(languageCode, cancellationToken);
        return items.Select(item => ToDto(item, ResolveName(item.IngredientId, item.Ingredient.Name, languageCode, dictionary))).ToArray();
    }

    public async Task<ShoppingListOperationResult> CreateAsync(Guid houseId, CreateShoppingListItemCommand command, string languageCode, CancellationToken cancellationToken)
    {
        if (!ValidQuantity(command.Quantity)) return InvalidQuantity();
        if (!await repository.IngredientExistsAsync(command.IngredientId, cancellationToken))
            return new(ShoppingListOperationStatus.NotFound);
        if (await repository.ItemExistsAsync(houseId, command.IngredientId, cancellationToken))
            return new(ShoppingListOperationStatus.Conflict, Message: "This ingredient is already on the shopping list.");

        var item = new ShoppingListItem { Id = Guid.NewGuid(), HouseId = houseId, IngredientId = command.IngredientId, Quantity = Quantity.FromAmount(command.Quantity) };
        repository.Add(item);
        await repository.SaveChangesAsync(cancellationToken);
        return new(ShoppingListOperationStatus.Success, await ToLocalizedDtoAsync((await repository.GetByIdAsync(item.Id, houseId, cancellationToken))!, languageCode, cancellationToken));
    }

    public async Task<ShoppingListOperationResult> UpdateAsync(Guid id, Guid houseId, UpdateShoppingListItemCommand command, string languageCode, CancellationToken cancellationToken)
    {
        if (!ValidQuantity(command.Quantity)) return InvalidQuantity();
        var item = await repository.GetByIdAsync(id, houseId, cancellationToken);
        if (item is null) return new(ShoppingListOperationStatus.NotFound);
        item.Quantity = Quantity.FromAmount(command.Quantity);
        item.IsPurchased = command.IsPurchased;
        await repository.SaveChangesAsync(cancellationToken);
        return new(ShoppingListOperationStatus.Success, await ToLocalizedDtoAsync(item, languageCode, cancellationToken));
    }

    public async Task<ShoppingListOperationStatus> DeleteAsync(Guid id, Guid houseId, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, houseId, cancellationToken);
        if (item is null) return ShoppingListOperationStatus.NotFound;
        repository.Remove(item);
        await repository.SaveChangesAsync(cancellationToken);
        return ShoppingListOperationStatus.Success;
    }

    public Task<int> ClearPurchasedAsync(Guid houseId, CancellationToken cancellationToken) =>
        repository.RemovePurchasedAsync(houseId, cancellationToken);

    private static bool ValidQuantity(decimal quantity) => quantity > 0 && quantity <= 99999999.999m;
    private static ShoppingListOperationResult InvalidQuantity() => new(ShoppingListOperationStatus.ValidationFailed,
        ValidationErrors: new Dictionary<string, string[]> { ["quantity"] = ["Quantity must be greater than 0 and no more than 99999999.999."] });
    private async Task<ShoppingListItemDto> ToLocalizedDtoAsync(ShoppingListItem item, string languageCode, CancellationToken cancellationToken)
    {
        var dictionary = await LoadTranslationsAsync(languageCode, cancellationToken);
        return ToDto(item, ResolveName(item.IngredientId, item.Ingredient.Name, languageCode, dictionary));
    }

    private Task<IReadOnlyDictionary<string, string>> LoadTranslationsAsync(string languageCode, CancellationToken cancellationToken) =>
        TranslationKeys.IsDefaultLanguage(languageCode)
            ? Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>())
            : translations.GetAllAsync(cancellationToken);

    private static string ResolveName(Guid ingredientId, string fallback, string languageCode, IReadOnlyDictionary<string, string> dictionary) =>
        !TranslationKeys.IsDefaultLanguage(languageCode)
        && dictionary.TryGetValue(TranslationKeys.Ingredient(ingredientId, languageCode), out var translated)
        && !string.IsNullOrWhiteSpace(translated) ? translated : fallback;

    private static ShoppingListItemDto ToDto(ShoppingListItem item, string ingredientName) => new(
        item.Id, item.IngredientId, ingredientName, item.Ingredient.MeasurementUnit,
        item.Quantity.Amount, item.Ingredient.PricePer100BaseUnits.Amount,
        (item.Quantity.Amount / 100m * item.Ingredient.PricePer100BaseUnits).RoundedToCents().Amount, item.IsPurchased,
        item.Ingredient.Category);
}
