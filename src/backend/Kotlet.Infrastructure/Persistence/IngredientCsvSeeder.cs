using System.Globalization;
using Kotlet.Domain.Common;
using Kotlet.Domain.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kotlet.Infrastructure.Persistence;

public sealed class IngredientCsvSeeder(
    KotletDbContext dbContext,
    ILogger<IngredientCsvSeeder> logger)
{
    internal const string RelativeFilePath = "SeedData/ingredients.csv";

    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Ingredients.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Ingredient seeding skipped; the table is not empty");
            return 0;
        }

        var path = Path.Combine(AppContext.BaseDirectory, RelativeFilePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("The ingredient seed CSV was not copied to the application output.", path);

        var seeds = await ReadAsync(path, cancellationToken);
        var ingredients = seeds
            .Select(seed =>
            {
                var classification = SeedIngredientDefaults.Classification(seed.Name);
                return new Ingredient
                {
                    Id = Guid.NewGuid(),
                    Name = seed.Name,
                    MeasurementUnit = seed.MeasurementUnit,
                    IsCountable = seed.MeasurementUnitsPerPiece.HasValue,
                    MeasurementUnitsPerPiece = seed.MeasurementUnitsPerPiece,
                    CaloriesPer100BaseUnits = Calories.FromKilocalories(seed.CaloriesPer100BaseUnits),
                    PricePer100BaseUnits = Price.FromAmount(seed.PricePer100BaseUnits),
                    Category = classification.Category,
                    Allergens = classification.Allergens,
                    Attributes = classification.Attributes,
                    Suitability = classification.Suitability
                };
            })
            .ToList();

        dbContext.Ingredients.AddRange(ingredients);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created {IngredientCount} ingredients from {SeedFile}", ingredients.Count, path);
        return ingredients.Count;
    }

    internal static async Task<IReadOnlyList<IngredientSeed>> ReadAsync(
        string path, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        if (lines.Length == 0 || lines[0].TrimStart('\uFEFF') != "name,measurement_unit,calories_per_100_base_units,price_per_100_base_units")
            throw new InvalidDataException($"Ingredient seed CSV '{path}' has an invalid header.");

        var result = new List<IngredientSeed>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var fields = line.Split(',');
            if (fields.Length != 4)
                throw InvalidRow(path, index + 1, "expected exactly four comma-separated fields");

            var name = fields[0].Trim();
            var unit = fields[1].Trim().ToLowerInvariant();
            if (name.Length is 0 or > 150)
                throw InvalidRow(path, index + 1, "name must contain 1-150 characters");
            if (unit.Length is 0 or > 30)
                throw InvalidRow(path, index + 1, "measurement unit must contain 1-30 characters");
            if (!decimal.TryParse(fields[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var calories) || calories < 0)
                throw InvalidRow(path, index + 1, "calories must be a non-negative invariant decimal");
            if (!decimal.TryParse(fields[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
                throw InvalidRow(path, index + 1, "price must be a non-negative invariant decimal");
            if (!names.Add(name))
                throw InvalidRow(path, index + 1, $"duplicate ingredient name '{name}'");

            result.Add(new IngredientSeed(name, unit, SeedIngredientDefaults.MeasurementUnitsPerPiece(name, unit), calories, price));
        }

        return result;
    }

    private static InvalidDataException InvalidRow(string path, int line, string reason) =>
        new($"Invalid ingredient seed CSV '{path}' at line {line}: {reason}.");

    internal sealed record IngredientSeed(
        string Name, string MeasurementUnit, decimal? MeasurementUnitsPerPiece,
        decimal CaloriesPer100BaseUnits, decimal PricePer100BaseUnits);
}
