using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.Ingredients;

public sealed class IngredientService(IIngredientRepository repository)
{
    private static readonly HashSet<string> MeasurementUnits = ["g", "ml"];

    public async Task<IReadOnlyCollection<IngredientDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var ingredients = await repository.GetAllAsync(cancellationToken);
        return ingredients.Select(ToDto).ToArray();
    }

    public async Task<IngredientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var ingredient = await repository.GetByIdAsync(id, tracked: false, cancellationToken);
        return ingredient is null ? null : ToDto(ingredient);
    }

    public async Task<IngredientOperationResult> CreateAsync(
        SaveIngredientCommand command,
        CancellationToken cancellationToken)
    {
        var errors = Validate(command);
        if (errors.Count > 0)
            return new(IngredientOperationStatus.ValidationFailed, ValidationErrors: errors);

        var name = command.Name.Trim();
        if (await repository.NameExistsAsync(name, null, cancellationToken))
            return Conflict();

        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = name,
            MeasurementUnit = NormalizeUnit(command.MeasurementUnit),
            IsCountable = command.IsCountable,
            MeasurementUnitsPerPiece = command.IsCountable ? command.MeasurementUnitsPerPiece : null,
            CaloriesPer100BaseUnits = command.CaloriesPer100BaseUnits,
            PricePer100BaseUnits = command.PricePer100BaseUnits
        };
        repository.Add(ingredient);
        await repository.SaveChangesAsync(cancellationToken);
        return new(IngredientOperationStatus.Success, ToDto(ingredient));
    }

    public async Task<IngredientOperationResult> UpdateAsync(
        Guid id,
        SaveIngredientCommand command,
        CancellationToken cancellationToken)
    {
        var errors = Validate(command);
        if (errors.Count > 0)
            return new(IngredientOperationStatus.ValidationFailed, ValidationErrors: errors);

        var ingredient = await repository.GetByIdAsync(id, tracked: true, cancellationToken);
        if (ingredient is null)
            return new(IngredientOperationStatus.NotFound);

        var name = command.Name.Trim();
        if (await repository.NameExistsAsync(name, id, cancellationToken))
            return Conflict();
        var measurementUnit = NormalizeUnit(command.MeasurementUnit);
        if (ingredient.MeasurementUnit != measurementUnit && await repository.IsInUseAsync(id, cancellationToken))
            return new(IngredientOperationStatus.Conflict,
                Message: "The base measurement unit cannot be changed while the ingredient is in use.");

        ingredient.Name = name;
        ingredient.MeasurementUnit = measurementUnit;
        ingredient.IsCountable = command.IsCountable;
        ingredient.MeasurementUnitsPerPiece = command.IsCountable ? command.MeasurementUnitsPerPiece : null;
        ingredient.CaloriesPer100BaseUnits = command.CaloriesPer100BaseUnits;
        ingredient.PricePer100BaseUnits = command.PricePer100BaseUnits;
        await repository.SaveChangesAsync(cancellationToken);
        return new(IngredientOperationStatus.Success, ToDto(ingredient));
    }

    public async Task<IngredientOperationStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var ingredient = await repository.GetByIdAsync(id, tracked: true, cancellationToken);
        if (ingredient is null)
            return IngredientOperationStatus.NotFound;
        if (await repository.IsInUseAsync(id, cancellationToken))
            return IngredientOperationStatus.Conflict;

        repository.Remove(ingredient);
        await repository.SaveChangesAsync(cancellationToken);
        return IngredientOperationStatus.Success;
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
    private static IngredientDto ToDto(Ingredient ingredient) =>
        new(ingredient.Id, ingredient.Name, ingredient.MeasurementUnit, ingredient.IsCountable,
            ingredient.MeasurementUnitsPerPiece, ingredient.CaloriesPer100BaseUnits, ingredient.PricePer100BaseUnits);
    private static IngredientOperationResult Conflict() =>
        new(IngredientOperationStatus.Conflict, Message: "An ingredient with this name already exists.");
}
