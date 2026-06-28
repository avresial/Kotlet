using Kotlet.Domain.Pantry;
using Kotlet.Application.Translations;

namespace Kotlet.Application.Pantry;

public sealed class PantryService(IPantryRepository repository, ITranslationRepository translations)
{
    public async Task<IReadOnlyCollection<PantryItemDto>> GetAllAsync(Guid houseId, string languageCode, CancellationToken cancellationToken)
    {
        var items = await repository.GetAllAsync(houseId, cancellationToken);
        var dictionary = await LoadTranslationsAsync(languageCode, cancellationToken);
        return items.Select(item => ToDto(item, ResolveName(item.IngredientId, item.Ingredient.Name, languageCode, dictionary))).ToArray();
    }

    public async Task<PantryOperationResult> CreateAsync(Guid houseId, SavePantryItemCommand command, string languageCode, CancellationToken cancellationToken)
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
        return new(PantryOperationStatus.Success, await ToLocalizedDtoAsync(saved!, languageCode, cancellationToken));
    }

    public async Task<PantryOperationResult> UpdateAsync(Guid id, Guid houseId, decimal quantity, string languageCode, CancellationToken cancellationToken)
    {
        if (quantity < 0 || quantity > 99999999.999m)
            return InvalidQuantity();
        var item = await repository.GetByIdAsync(id, houseId, cancellationToken);
        if (item is null) return new(PantryOperationStatus.NotFound);
        item.Quantity = quantity;
        await repository.SaveChangesAsync(cancellationToken);
        return new(PantryOperationStatus.Success, await ToLocalizedDtoAsync(item, languageCode, cancellationToken));
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
    private async Task<PantryItemDto> ToLocalizedDtoAsync(PantryItem item, string languageCode, CancellationToken cancellationToken)
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

    private static PantryItemDto ToDto(PantryItem item, string ingredientName) =>
        new(item.Id, item.IngredientId, ingredientName, item.Ingredient.MeasurementUnit, item.Quantity);
}
