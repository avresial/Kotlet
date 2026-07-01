namespace Kotlet.Domain.Ingredients;

/// <summary>Persisted schema: never reorder or remove members; only append using bits 0-62.</summary>
[Flags]
public enum FoodAttribute : long
{
    None = 0,
    AnimalOrigin = 1L << 0, PlantOrigin = 1L << 1,
    ContainsLactose = 1L << 2, ContainsAlcohol = 1L << 3, ContainsCaffeine = 1L << 4,
    HighHistamine = 1L << 5, HighFodmap = 1L << 6,
    Fermented = 1L << 7, Smoked = 1L << 8, Spicy = 1L << 9,
    Processed = 1L << 10, UltraProcessed = 1L << 11
}
