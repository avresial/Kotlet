using Kotlet.Application.Translations;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.Ingredients;

public sealed class IngredientService(
    IIngredientRepository repository,
    ITranslationRepository translations,
    IIngredientTranslationSignal translationSignal)
{
    private static readonly HashSet<string> MeasurementUnits = ["g", "ml"];
    private static readonly IReadOnlyDictionary<string, string> NoTranslations =
        new Dictionary<string, string>();
    private static readonly Allergen KnownAllergens = Enum.GetValues<Allergen>().Aggregate((left, right) => left | right);
    private static readonly FoodAttribute KnownAttributes = Enum.GetValues<FoodAttribute>().Aggregate((left, right) => left | right);
    private static readonly DietarySuitability KnownSuitability = Enum.GetValues<DietarySuitability>().Aggregate((left, right) => left | right);

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
            .Select(ingredient => ToDto(ingredient, ResolveTranslation(ingredient, languageCode, dictionary)))
            .OrderBy(dto => dto.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IngredientDto?> GetByIdAsync(Guid id, string languageCode, CancellationToken cancellationToken)
    {
        var ingredient = await repository.GetByIdAsync(id, tracked: false, cancellationToken);
        if (ingredient is null)
            return null;
        var dictionary = await LoadTranslationsAsync(languageCode, cancellationToken);
        return ToDto(ingredient, ResolveTranslation(ingredient, languageCode, dictionary));
    }

    public async Task<IngredientOperationResult> CreateAsync(
        SaveIngredientCommand command,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var errors = Validate(command);
        if (errors.Count > 0)
            return new(IngredientOperationStatus.ValidationFailed, ValidationErrors: errors);

        var isDefaultLanguage = TranslationKeys.IsDefaultLanguage(languageCode);
        var (canonicalName, persistedTranslation) = ResolveNames(command, isDefaultLanguage);
        var displayName = isDefaultLanguage ? canonicalName : persistedTranslation!;
        if (await IsDisplayNameTakenAsync(displayName, languageCode, null, cancellationToken))
            return Conflict();

        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = canonicalName,
            MeasurementUnit = NormalizeUnit(command.MeasurementUnit),
            IsCountable = command.IsCountable,
            MeasurementUnitsPerPiece = command.IsCountable ? command.MeasurementUnitsPerPiece : null,
            CaloriesPer100BaseUnits = Calories.FromKilocalories(command.CaloriesPer100BaseUnits),
            PricePer100BaseUnits = Price.FromAmount(command.PricePer100BaseUnits),
            Category = command.Category, Allergens = command.Allergens,
            Attributes = command.Attributes, Suitability = command.Suitability,
            IsAiModified = command.IsAiModified,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        repository.Add(ingredient);
        // Stage the translation (when any) so the ingredient row and its translation are persisted
        // in a single commit on the shared DbContext, keeping the two writes atomic.
        if (persistedTranslation is not null)
            await translations.SetAsync(TranslationKeys.Ingredient(ingredient.Id, languageCode), persistedTranslation, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        // Wake the background worker so the languages this ingredient still lacks get translated.
        translationSignal.Notify();

        return new(IngredientOperationStatus.Success, ToDto(ingredient, isDefaultLanguage ? null : persistedTranslation));
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

        var isDefaultLanguage = TranslationKeys.IsDefaultLanguage(languageCode);
        var (canonicalName, persistedTranslation) = ResolveNames(command, isDefaultLanguage);
        var displayName = isDefaultLanguage ? canonicalName : persistedTranslation!;
        if (await IsDisplayNameTakenAsync(displayName, languageCode, id, cancellationToken))
            return Conflict();
        var measurementUnit = NormalizeUnit(command.MeasurementUnit);
        if (ingredient.MeasurementUnit != measurementUnit && await repository.IsInUseAsync(id, cancellationToken))
            return new(IngredientOperationStatus.Conflict,
                Message: "The base measurement unit cannot be changed while the ingredient is in use.");

        // The canonical (default-language) name is only set when we know it: when editing in the
        // default language, or when a non-default editor also supplied the canonical name alongside
        // the translation. A legacy translation-only edit leaves the existing canonical name intact.
        if (isDefaultLanguage || !string.IsNullOrWhiteSpace(command.Translation))
            ingredient.Name = canonicalName;
        ingredient.MeasurementUnit = measurementUnit;
        ingredient.IsCountable = command.IsCountable;
        ingredient.MeasurementUnitsPerPiece = command.IsCountable ? command.MeasurementUnitsPerPiece : null;
        ingredient.CaloriesPer100BaseUnits = Calories.FromKilocalories(command.CaloriesPer100BaseUnits);
        ingredient.PricePer100BaseUnits = Price.FromAmount(command.PricePer100BaseUnits);
        ingredient.Category = command.Category;
        ingredient.Allergens = command.Allergens;
        ingredient.Attributes = command.Attributes;
        ingredient.Suitability = command.Suitability;
        ingredient.IsAiModified = false;
        if (persistedTranslation is not null)
            await translations.SetAsync(TranslationKeys.Ingredient(ingredient.Id, languageCode), persistedTranslation, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new(IngredientOperationStatus.Success, ToDto(ingredient, isDefaultLanguage ? null : persistedTranslation));
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

    /// <summary>
    /// Splits the submitted command into the canonical (default-language) name stored on the entity
    /// and the translation (when any) stored in the dictionary for the editor's language.
    /// </summary>
    private static (string CanonicalName, string? PersistedTranslation) ResolveNames(SaveIngredientCommand command, bool isDefaultLanguage)
    {
        var inputName = command.Name.Trim();
        if (isDefaultLanguage)
            return (inputName, null);
        var translation = command.Translation?.Trim();
        // The editor supplied both the canonical name and its translation (the new edit flow).
        if (!string.IsNullOrWhiteSpace(translation))
            return (inputName, translation);
        // Legacy flow: only a name was supplied in a non-default language, so it *is* the translation
        // and the canonical name stays the "Unknown" placeholder until an English name is provided.
        return (UnknownName, inputName);
    }

    private static string ResolveName(Ingredient ingredient, string languageCode, IReadOnlyDictionary<string, string> dictionary) =>
        ResolveTranslation(ingredient, languageCode, dictionary) is { } translated ? translated : ingredient.Name;

    private static string? ResolveTranslation(Ingredient ingredient, string languageCode, IReadOnlyDictionary<string, string> dictionary)
    {
        if (TranslationKeys.IsDefaultLanguage(languageCode))
            return null;
        return dictionary.TryGetValue(TranslationKeys.Ingredient(ingredient.Id, languageCode), out var translated)
               && !string.IsNullOrWhiteSpace(translated)
            ? translated
            : null;
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
        if (!Enum.IsDefined(command.Category))
            errors["category"] = ["Category is invalid."];
        if ((command.Allergens & ~KnownAllergens) != 0)
            errors["allergens"] = ["Allergens contain unsupported values."];
        if ((command.Attributes & ~KnownAttributes) != 0)
            errors["attributes"] = ["Attributes contain unsupported values."];
        if ((command.Suitability & ~KnownSuitability) != 0)
            errors["suitability"] = ["Suitability contains unsupported values."];
        return errors;
    }

    private static string NormalizeUnit(string unit) => unit.Trim().ToLowerInvariant();
    private static IngredientDto ToDto(Ingredient ingredient, string? translation) =>
        new(ingredient.Id,
            string.IsNullOrWhiteSpace(translation) ? ingredient.Name : translation,
            ingredient.Name,
            translation,
            ingredient.MeasurementUnit, ingredient.IsCountable,
            ingredient.MeasurementUnitsPerPiece, ingredient.CaloriesPer100BaseUnits.Kilocalories,
            ingredient.PricePer100BaseUnits.Amount, ingredient.SvgIcon, ingredient.Category,
            ingredient.Allergens, ingredient.Attributes, ingredient.Suitability, ingredient.IsAiModified, ingredient.CreatedAtUtc);
    private static IngredientOperationResult Conflict() =>
        new(IngredientOperationStatus.Conflict, Message: "An ingredient with this name already exists.");
}
