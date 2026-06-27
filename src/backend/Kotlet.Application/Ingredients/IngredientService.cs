using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.Ingredients;

public sealed class IngredientService(IIngredientRepository repository)
{
    private static readonly HashSet<string> MeasurementUnits =
    [
        "g", "kg", "ml", "l", "piece", "tsp", "tbsp", "cup"
    ];

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
            CaloriesPer100Grams = command.CaloriesPer100Grams,
            Price = command.Price
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

        ingredient.Name = name;
        ingredient.MeasurementUnit = NormalizeUnit(command.MeasurementUnit);
        ingredient.CaloriesPer100Grams = command.CaloriesPer100Grams;
        ingredient.Price = command.Price;
        await repository.SaveChangesAsync(cancellationToken);
        return new(IngredientOperationStatus.Success, ToDto(ingredient));
    }

    public async Task<IngredientOperationStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var ingredient = await repository.GetByIdAsync(id, tracked: true, cancellationToken);
        if (ingredient is null)
            return IngredientOperationStatus.NotFound;

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
        if (command.CaloriesPer100Grams < 0 || command.CaloriesPer100Grams > 999999.99m)
            errors["caloriesPer100Grams"] = ["Calories per 100 grams must be between 0 and 999999.99."];
        if (command.Price < 0 || command.Price > 99999999.99m)
            errors["price"] = ["Price must be between 0 and 99999999.99."];
        return errors;
    }

    private static string NormalizeUnit(string unit) => unit.Trim().ToLowerInvariant();
    private static IngredientDto ToDto(Ingredient ingredient) =>
        new(ingredient.Id, ingredient.Name, ingredient.MeasurementUnit, ingredient.CaloriesPer100Grams, ingredient.Price);
    private static IngredientOperationResult Conflict() =>
        new(IngredientOperationStatus.Conflict, Message: "An ingredient with this name already exists.");
}
