using Kotlet.Application.Translations;
using Kotlet.Domain.Common;
using Kotlet.Domain.Shopping;

namespace Kotlet.Application.Shopping;

public sealed class ShoppingListService(IShoppingListRepository repository, ITranslationRepository translations)
{
    private static readonly IReadOnlyDictionary<string, string> NoTranslations = new Dictionary<string, string>();

    public async Task<IReadOnlyCollection<ShoppingListItemDto>> GetAllAsync(Guid houseId, string languageCode, CancellationToken cancellationToken)
    {
        var items = await repository.GetAllAsync(houseId, cancellationToken);
        var dictionary = await LoadTranslationsAsync(languageCode, cancellationToken);
        return items.Select(item => ToDto(item, ResolveName(item, languageCode, dictionary))).ToArray();
    }

    public async Task<ShoppingListOperationResult> CreateAsync(Guid houseId, CreateShoppingListItemCommand command, string languageCode, CancellationToken cancellationToken)
    {
        if (!ValidQuantity(command.Quantity))
        {
            return InvalidQuantity();
        }

        if (!ValidNote(command.Note))
        {
            return InvalidNote();
        }

        var customName = NormalizeCustomName(command.CustomName);
        if (!ValidCustomName(command.CustomName, customName))
        {
            return InvalidCustomName();
        }

        var sourceCount = (command.IngredientId is not null ? 1 : 0)
            + (command.PreparedMealId is not null ? 1 : 0)
            + (customName is not null ? 1 : 0);
        if (sourceCount != 1)
        {
            return new(ShoppingListOperationStatus.ValidationFailed, ValidationErrors:
                new Dictionary<string, string[]> { ["item"] = ["Choose exactly one of an ingredient, a ready meal, or a custom item."] });
        }

        if (command.IngredientId is { } ingredientId && !await repository.IngredientExistsAsync(ingredientId, cancellationToken))
        {
            return new(ShoppingListOperationStatus.NotFound);
        }

        if (command.PreparedMealId is { } preparedMealId && !await repository.PreparedMealExistsAsync(preparedMealId, houseId, cancellationToken))
        {
            return new(ShoppingListOperationStatus.NotFound);
        }

        if (customName is null && await repository.ItemExistsAsync(houseId, command.IngredientId, command.PreparedMealId, cancellationToken))
        {
            return new(ShoppingListOperationStatus.Conflict, Message: "This product is already on the shopping list.");
        }

        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            IngredientId = command.IngredientId,
            PreparedMealId = command.PreparedMealId,
            CustomName = customName,
            Quantity = Quantity.FromAmount(command.Quantity),
            Note = NormalizeNote(command.Note)
        };
        repository.Add(item);
        await repository.SaveChangesAsync(cancellationToken);

        // Map the inserted entity instead of reloading it: a concurrent delete between the
        // insert and a reload would otherwise turn a successful create into a failure.
        await repository.LoadReferencesAsync(item, cancellationToken);

        return new(
            ShoppingListOperationStatus.Success,
            await ToLocalizedDtoAsync(item, languageCode, cancellationToken));
    }

    public async Task<ShoppingListOperationResult> UpdateAsync(Guid id, Guid houseId, UpdateShoppingListItemCommand command, string languageCode, CancellationToken cancellationToken)
    {
        if (!ValidQuantity(command.Quantity))
        {
            return InvalidQuantity();
        }

        if (!ValidNote(command.Note))
        {
            return InvalidNote();
        }

        var item = await repository.GetByIdAsync(id, houseId, cancellationToken);
        if (item is null)
        {
            return new(ShoppingListOperationStatus.NotFound);
        }

        item.Quantity = Quantity.FromAmount(command.Quantity);
        item.IsPurchased = command.IsPurchased;
        // Note is a partial field: omitting it (null) preserves the stored value; an
        // explicit (possibly empty) string replaces it, clearing when blank.
        if (command.Note is not null)
        {
            item.Note = NormalizeNote(command.Note);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return new(ShoppingListOperationStatus.Success, await ToLocalizedDtoAsync(item, languageCode, cancellationToken));
    }

    public async Task<ShoppingListOperationStatus> DeleteAsync(Guid id, Guid houseId, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, houseId, cancellationToken);
        if (item is null)
        {
            return ShoppingListOperationStatus.NotFound;
        }

        repository.Remove(item);
        await repository.SaveChangesAsync(cancellationToken);
        return ShoppingListOperationStatus.Success;
    }

    public Task<int> ClearPurchasedAsync(Guid houseId, CancellationToken cancellationToken) =>
        repository.RemovePurchasedAsync(houseId, cancellationToken);

    public async Task<ShoppingListOperationResult> GenerateAsync(
        Guid houseId, GenerateShoppingListCommand command, string languageCode, CancellationToken cancellationToken)
    {
        if (command.To < command.From || command.To.DayNumber - command.From.DayNumber > 31)
        {
            return new(ShoppingListOperationStatus.ValidationFailed, ValidationErrors:
                new Dictionary<string, string[]> { ["to"] = ["Date range must contain between 1 and 32 days."] });
        }

        var planned = await repository.GetPlannedIngredientsAsync(houseId, command.From, command.To, cancellationToken);
        var existing = (await repository.GetAllTrackedAsync(houseId, cancellationToken))
            .Where(item => item.IngredientId is not null)
            .ToDictionary(item => item.IngredientId!.Value);
        foreach (var ingredient in planned.Where(item => item.Quantity > 0))
        {
            if (existing.TryGetValue(ingredient.IngredientId, out var item))
            {
                item.Quantity += Quantity.FromAmount(ingredient.Quantity);
                item.IsPurchased = false;
            }
            else
            {
                repository.Add(new ShoppingListItem
                {
                    Id = Guid.NewGuid(),
                    HouseId = houseId,
                    IngredientId = ingredient.IngredientId,
                    Quantity = Quantity.FromAmount(ingredient.Quantity)
                });
            }
        }

        if (planned.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return new(ShoppingListOperationStatus.Success,
            Items: await GetAllAsync(houseId, languageCode, cancellationToken));
    }

    private const int MaxNoteLength = 500;
    private const int MaxCustomNameLength = 500;
    private static bool ValidQuantity(decimal quantity) => quantity > 0 && quantity <= 99999999.999m;
    private static ShoppingListOperationResult InvalidQuantity() => new(ShoppingListOperationStatus.ValidationFailed,
        ValidationErrors: new Dictionary<string, string[]> { ["quantity"] = ["Quantity must be greater than 0 and no more than 99999999.999."] });
    private static bool ValidNote(string? note) => note is null || note.Trim().Length <= MaxNoteLength;
    private static ShoppingListOperationResult InvalidNote() => new(ShoppingListOperationStatus.ValidationFailed,
        ValidationErrors: new Dictionary<string, string[]> { ["note"] = [$"Note must be no more than {MaxNoteLength} characters."] });
    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    private static string? NormalizeCustomName(string? customName) => customName?.Trim();
    private static bool ValidCustomName(string? original, string? normalized) =>
        original is null || normalized is { Length: > 0 and <= MaxCustomNameLength };
    private static ShoppingListOperationResult InvalidCustomName() => new(ShoppingListOperationStatus.ValidationFailed,
        ValidationErrors: new Dictionary<string, string[]> { ["customName"] = [$"Custom item name must be between 1 and {MaxCustomNameLength} characters."] });
    private async Task<ShoppingListItemDto> ToLocalizedDtoAsync(ShoppingListItem item, string languageCode, CancellationToken cancellationToken)
    {
        var dictionary = await LoadTranslationsAsync(languageCode, cancellationToken);
        return ToDto(item, ResolveName(item, languageCode, dictionary));
    }

    private Task<IReadOnlyDictionary<string, string>> LoadTranslationsAsync(string languageCode, CancellationToken cancellationToken) =>
        TranslationKeys.IsDefaultLanguage(languageCode)
            ? Task.FromResult(NoTranslations)
            : translations.GetAllAsync(cancellationToken);

    private static string ResolveName(ShoppingListItem item, string languageCode, IReadOnlyDictionary<string, string> dictionary)
    {
        if (item.CustomName is { } customName)
        {
            return customName;
        }

        if (item.PreparedMeal is { } preparedMeal)
        {
            return preparedMeal.Name;
        }

        var ingredient = item.Ingredient
            ?? throw new InvalidOperationException("Shopping-list item is missing its ingredient.");
        return !TranslationKeys.IsDefaultLanguage(languageCode)
               && dictionary.TryGetValue(TranslationKeys.Ingredient(ingredient.Id, languageCode), out var translated)
               && !string.IsNullOrWhiteSpace(translated)
            ? translated
            : ingredient.Name;
    }

    private static ShoppingListItemDto ToDto(ShoppingListItem item, string name)
    {
        if (item.CustomName is not null)
        {
            return new(item.Id, null, null, name, "package", item.Quantity.Amount, 0m, 0m,
                item.IsPurchased, Kotlet.Domain.Ingredients.FoodCategory.Unknown, item.Note, item.CustomName);
        }

        if (item.PreparedMeal is { } meal)
        {
            var unitPrice = meal.Price ?? 0m;
            return new(item.Id, null, meal.Id, name, "package", item.Quantity.Amount, unitPrice,
                decimal.Round(item.Quantity.Amount * unitPrice, 2, MidpointRounding.AwayFromZero),
                item.IsPurchased, Kotlet.Domain.Ingredients.FoodCategory.Unknown, item.Note);
        }

        var ingredient = item.Ingredient
            ?? throw new InvalidOperationException("Shopping-list item is missing its ingredient.");
        return new(item.Id, ingredient.Id, null, name, ingredient.MeasurementUnit,
            item.Quantity.Amount, ingredient.PricePer100BaseUnits.Amount,
            (item.Quantity.Amount / 100m * ingredient.PricePer100BaseUnits).RoundedToCents().Amount, item.IsPurchased,
            ingredient.Category, item.Note);
    }
}
