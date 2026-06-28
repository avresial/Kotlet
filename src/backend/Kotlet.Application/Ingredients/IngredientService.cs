using Kotlet.Application.Translations;
using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.Ingredients;

public sealed class IngredientService(IIngredientRepository repository, ITranslationRepository translations)
{
    private static readonly HashSet<string> MeasurementUnits = ["g", "ml"];
    private static readonly IReadOnlyDictionary<string, string> NoTranslations =
        new Dictionary<string, string>();

    /// <summary>
    /// The canonical name stored for an ingredient that was created in a non-default language.
    /// The real name lives in the translation dictionary; the default-language name stays
    /// "Unknown" until an (English) translation is provided.
    /// </summary>
    private const string UnknownName = "Unknown";

    public async Task<IReadOnlyCollection<IngredientDto>> GetAllAsync(string languageCode, CancellationToken cancellationToken)
    {
        var ingredients = await repository.GetAllAsync(cancellationToken);
        var dictionary = await LoadTranslationsAsync(languageCode, cancellationToken);
        return ingredients
            .Select(ingredient => ToDto(ingredient, ResolveName(ingredient, languageCode, dictionary)))
            .OrderBy(dto => dto.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IngredientDto?> GetByIdAsync(Guid id, string languageCode, CancellationToken cancellationToken)
    {
        var ingredient = await repository.GetByIdAsync(id, tracked: false, cancellationToken);
        if (ingredient is null)
            return null;
        var dictionary = await LoadTranslationsAsync(languageCode, cancellationToken);
        return ToDto(ingredient, ResolveName(ingredient, languageCode, dictionary));
    }

    public async Task<IngredientOperationResult> CreateAsync(
        SaveIngredientCommand command,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var errors = Validate(command);
        if (errors.Count > 0)
            return new(IngredientOperationStatus.ValidationFailed, ValidationErrors: errors);

        var displayName = command.Name.Trim();
        if (await IsDisplayNameTakenAsync(displayName, languageCode, null, cancellationToken))
            return Conflict();

        var isDefaultLanguage = TranslationKeys.IsDefaultLanguage(languageCode);
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = isDefaultLanguage ? displayName : UnknownName,
            MeasurementUnit = NormalizeUnit(command.MeasurementUnit),
            IsCountable = command.IsCountable,
            MeasurementUnitsPerPiece = command.IsCountable ? command.MeasurementUnitsPerPiece : null,
            CaloriesPer100BaseUnits = command.CaloriesPer100BaseUnits,
            PricePer100BaseUnits = command.PricePer100BaseUnits
        };
        repository.Add(ingredient);
        // Stage the translation (when any) so the ingredient row and its translation are persisted
        // in a single commit on the shared DbContext, keeping the two writes atomic.
        if (!isDefaultLanguage)
            await translations.SetAsync(TranslationKeys.Ingredient(ingredient.Id, languageCode), displayName, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new(IngredientOperationStatus.Success, ToDto(ingredient, displayName));
    }

    public async Task<IngredientOperationResult> UpdateAsync(
        Guid id,
        SaveIngredientCommand command,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var errors = Validate(command);
        if (errors.Count > 0)
            return new(IngredientOperationStatus.ValidationFailed, ValidationErrors: errors);

        var ingredient = await repository.GetByIdAsync(id, tracked: true, cancellationToken);
        if (ingredient is null)
            return new(IngredientOperationStatus.NotFound);

        var displayName = command.Name.Trim();
        if (await IsDisplayNameTakenAsync(displayName, languageCode, id, cancellationToken))
            return Conflict();
        var measurementUnit = NormalizeUnit(command.MeasurementUnit);
        if (ingredient.MeasurementUnit != measurementUnit && await repository.IsInUseAsync(id, cancellationToken))
            return new(IngredientOperationStatus.Conflict,
                Message: "The base measurement unit cannot be changed while the ingredient is in use.");

        var isDefaultLanguage = TranslationKeys.IsDefaultLanguage(languageCode);
        if (isDefaultLanguage)
            ingredient.Name = displayName;
        ingredient.MeasurementUnit = measurementUnit;
        ingredient.IsCountable = command.IsCountable;
        ingredient.MeasurementUnitsPerPiece = command.IsCountable ? command.MeasurementUnitsPerPiece : null;
        ingredient.CaloriesPer100BaseUnits = command.CaloriesPer100BaseUnits;
        ingredient.PricePer100BaseUnits = command.PricePer100BaseUnits;
        if (!isDefaultLanguage)
            await translations.SetAsync(TranslationKeys.Ingredient(ingredient.Id, languageCode), displayName, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new(IngredientOperationStatus.Success, ToDto(ingredient, displayName));
    }

    public async Task<IngredientOperationStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var ingredient = await repository.GetByIdAsync(id, tracked: true, cancellationToken);
        if (ingredient is null)
            return IngredientOperationStatus.NotFound;
        if (await repository.IsInUseAsync(id, cancellationToken))
            return IngredientOperationStatus.Conflict;

        repository.Remove(ingredient);
        // Remove the ingredient and all of its translations in a single commit.
        await translations.RemoveByPrefixAsync(TranslationKeys.IngredientPrefix(id), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return IngredientOperationStatus.Success;
    }

    private Task<IReadOnlyDictionary<string, string>> LoadTranslationsAsync(string languageCode, CancellationToken cancellationToken) =>
        TranslationKeys.IsDefaultLanguage(languageCode)
            ? Task.FromResult(NoTranslations)
            : translations.GetAllAsync(cancellationToken);

    private async Task<bool> IsDisplayNameTakenAsync(string name, string languageCode, Guid? excludedId, CancellationToken cancellationToken)
    {
        var ingredients = await repository.GetAllAsync(cancellationToken);
        var dictionary = await LoadTranslationsAsync(languageCode, cancellationToken);
        return ingredients.Any(ingredient => ingredient.Id != excludedId &&
            string.Equals(ResolveName(ingredient, languageCode, dictionary), name, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveName(Ingredient ingredient, string languageCode, IReadOnlyDictionary<string, string> dictionary)
    {
        if (TranslationKeys.IsDefaultLanguage(languageCode))
            return ingredient.Name;
        return dictionary.TryGetValue(TranslationKeys.Ingredient(ingredient.Id, languageCode), out var translated)
               && !string.IsNullOrWhiteSpace(translated)
            ? translated
            : ingredient.Name;
    }

    private static Dictionary<string, string[]> Validate(SaveIngredientCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 150)
            errors["name"] = ["Name is required and cannot exceed 150 characters."];
        var unit = command.MeasurementUnit?.Trim().ToLowerInvariant();
        if (unit is null || !MeasurementUnits.Contains(unit))
            errors["measurementUnit"] = [$"Measurement unit must be one of: {string.Join(", ", MeasurementUnits)}."];
        if (command.IsCountable && (!command.MeasurementUnitsPerPiece.HasValue || command.MeasurementUnitsPerPiece <= 0 || command.MeasurementUnitsPerPiece > 999999999.999m))
            errors["measurementUnitsPerPiece"] = ["A countable ingredient must define a positive number of measurement units per piece."];
        if (command.CaloriesPer100BaseUnits < 0 || command.CaloriesPer100BaseUnits > 999999.99m)
            errors["caloriesPer100BaseUnits"] = ["Calories per 100 base units must be between 0 and 999999.99."];
        if (command.PricePer100BaseUnits < 0 || command.PricePer100BaseUnits > 99999999.99m)
            errors["pricePer100BaseUnits"] = ["Price per 100 base units must be between 0 and 99999999.99."];
        return errors;
    }

    private static string NormalizeUnit(string unit) => unit.Trim().ToLowerInvariant();
    private static IngredientDto ToDto(Ingredient ingredient, string name) =>
        new(ingredient.Id, name, ingredient.MeasurementUnit, ingredient.IsCountable,
            ingredient.MeasurementUnitsPerPiece, ingredient.CaloriesPer100BaseUnits,
            ingredient.PricePer100BaseUnits, ingredient.SvgIcon);
    private static IngredientOperationResult Conflict() =>
        new(IngredientOperationStatus.Conflict, Message: "An ingredient with this name already exists.");
}
