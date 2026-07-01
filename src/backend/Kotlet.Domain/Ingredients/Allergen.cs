namespace Kotlet.Domain.Ingredients;

/// <summary>Persisted schema: never reorder or remove members; only append using bits 0-62.</summary>
[Flags]
public enum Allergen : long
{
    None = 0,
    Gluten = 1L << 0, Crustaceans = 1L << 1, Eggs = 1L << 2, Fish = 1L << 3,
    Peanuts = 1L << 4, Soybeans = 1L << 5, Milk = 1L << 6, TreeNuts = 1L << 7,
    Celery = 1L << 8, Mustard = 1L << 9, Sesame = 1L << 10, Sulphites = 1L << 11,
    Lupin = 1L << 12, Molluscs = 1L << 13
}
