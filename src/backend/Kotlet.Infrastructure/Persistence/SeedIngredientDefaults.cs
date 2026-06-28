namespace Kotlet.Infrastructure.Persistence;

internal static class SeedIngredientDefaults
{
    private static readonly IReadOnlyDictionary<string, decimal> PieceWeights =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Apple"] = 180m,
            ["Pear"] = 180m,
            ["Banana"] = 120m,
            ["Orange"] = 150m,
            ["Mandarin"] = 90m,
            ["Lemon"] = 60m,
            ["Lime"] = 70m,
            ["Grapefruit"] = 230m,
            ["Peach"] = 150m,
            ["Nectarine"] = 140m,
            ["Apricot"] = 35m,
            ["Plum"] = 70m,
            ["Kiwi"] = 75m,
            ["Avocado"] = 200m,
            ["Chicken egg"] = 50m,
            ["Quail egg"] = 10m,
            ["Eggs"] = 50m
        };

    public static decimal? MeasurementUnitsPerPiece(string name, string measurementUnit) =>
        measurementUnit == "g" && PieceWeights.TryGetValue(name, out var weight) ? weight : null;
}
