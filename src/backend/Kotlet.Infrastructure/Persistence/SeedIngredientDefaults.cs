using Kotlet.Domain.Ingredients;

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

    public static (FoodCategory Category, Allergen Allergens, FoodAttribute Attributes, DietarySuitability Suitability)
        Classification(string name) => name switch
        {
            "Salmon fillet" => (FoodCategory.Fish, Allergen.Fish, FoodAttribute.AnimalOrigin, DietarySuitability.Pescatarian),
            "Shrimp" => (FoodCategory.Shellfish, Allergen.Crustaceans, FoodAttribute.AnimalOrigin, DietarySuitability.Pescatarian),
            "Whole milk" or "Skimmed milk" => (FoodCategory.Dairy, Allergen.Milk, FoodAttribute.AnimalOrigin | FoodAttribute.ContainsLactose, DietarySuitability.Vegetarian),
            "Parmesan" => (FoodCategory.Cheese, Allergen.Milk, FoodAttribute.AnimalOrigin | FoodAttribute.Fermented, DietarySuitability.None),
            "Pasta" => (FoodCategory.Grain, Allergen.Gluten, FoodAttribute.PlantOrigin, DietarySuitability.Vegan | DietarySuitability.Vegetarian),
            "Peanut butter" => (FoodCategory.Composite, Allergen.Peanuts, FoodAttribute.PlantOrigin | FoodAttribute.Processed, DietarySuitability.Vegan | DietarySuitability.Vegetarian),
            "Soy sauce" => (FoodCategory.Sauce, Allergen.Soybeans | Allergen.Gluten, FoodAttribute.PlantOrigin | FoodAttribute.Fermented, DietarySuitability.Vegan | DietarySuitability.Vegetarian),
            "Apple" => (FoodCategory.Fruit, Allergen.None, FoodAttribute.PlantOrigin, DietarySuitability.Vegan | DietarySuitability.Vegetarian),
            "Red lentils" or "Green lentils" or "Brown lentils" => (FoodCategory.Legume, Allergen.None, FoodAttribute.PlantOrigin, DietarySuitability.Vegan | DietarySuitability.Vegetarian),
            _ => default
        };
}
