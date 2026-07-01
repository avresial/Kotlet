namespace Kotlet.Domain.Ingredients;

/// <summary>Persisted schema: never reorder or remove members; only append using bits 0-62.</summary>
[Flags]
public enum DietarySuitability : long
{
    None = 0,
    Vegan = 1L << 0, Vegetarian = 1L << 1, Pescatarian = 1L << 2,
    GlutenFree = 1L << 3, LactoseFree = 1L << 4,
    LowFodmap = 1L << 5, LowHistamine = 1L << 6,
    Keto = 1L << 7, LowCarb = 1L << 8
}
