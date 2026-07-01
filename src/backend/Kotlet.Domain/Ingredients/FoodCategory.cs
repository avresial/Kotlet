namespace Kotlet.Domain.Ingredients;

/// <summary>Persisted schema: never reorder or remove members; only append.</summary>
public enum FoodCategory
{
    Unknown = 0,
    Meat = 1, Poultry = 2, Fish = 3, Shellfish = 4, Egg = 5, Dairy = 6, Cheese = 7,
    Vegetable = 20, Fruit = 21, Legume = 22, Grain = 23, Nut = 24, Seed = 25, Mushroom = 26,
    Herb = 40, Spice = 41,
    Oil = 60, Sweetener = 61, Condiment = 62, Sauce = 63, Beverage = 64,
    Composite = 90, Additive = 91
}
