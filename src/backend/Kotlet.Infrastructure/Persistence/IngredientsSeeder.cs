using System.Globalization;
using System.Reflection;
using Kotlet.Domain.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kotlet.Infrastructure.Persistence;

public sealed class IngredientsSeeder(
    KotletDbContext dbContext,
    ILogger<IngredientsSeeder> logger)
{
    private const string ResourceName = "Kotlet.Infrastructure.Resources.ingredients.csv";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var existingNames = await dbContext.Ingredients
            .Select(i => i.Name)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var ingredients = ParseIngredients()
            .Where(i => !existingNames.Contains(i.Name))
            .ToList();

        if (ingredients.Count == 0)
        {
            logger.LogInformation("Ingredients seeding skipped; all entries already exist");
            return;
        }

        dbContext.Ingredients.AddRange(ingredients);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Ingredients seeding completed; {Count} ingredients created", ingredients.Count);
    }

    private static IEnumerable<Ingredient> ParseIngredients()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);

        reader.ReadLine(); // skip header row

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(',');
            if (parts.Length < 4)
                continue;

            if (!decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var calories))
                continue;
            if (!decimal.TryParse(parts[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                continue;

            var name = parts[0].Trim();
            var unit = parts[1].Trim().ToLowerInvariant();
            if (unit == "piece") unit = "g";
            var pieceSize = SeedIngredientDefaults.MeasurementUnitsPerPiece(name, unit);
            yield return new Ingredient
            {
                Id = Guid.NewGuid(),
                Name = name,
                MeasurementUnit = unit,
                IsCountable = pieceSize.HasValue,
                MeasurementUnitsPerPiece = pieceSize,
                CaloriesPer100BaseUnits = calories,
                PricePer100BaseUnits = price
            };
        }
    }
}
