using System.Security.Cryptography;
using System.Text;

namespace Kotlet.TestData;

/// <summary>
/// Derives stable identifiers from names. Every row in the fixture gets the same id on every
/// build, which is what lets tests assert against a known id and lets the MCP benchmark compare
/// payload sizes byte for byte between runs.
/// </summary>
public static class TestIds
{
    /// <summary>
    /// A deterministic GUID for <paramref name="name"/> within <paramref name="scope"/>.
    /// Scoping keeps, say, a recipe and an ingredient of the same name from colliding.
    /// </summary>
    public static Guid For(string scope, string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"kotlet-test-data/{scope}/{name}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static Guid Ingredient(string name) => For("ingredient", name);
    public static Guid Recipe(string title) => For("recipe", title);
    public static Guid User(string email) => For("user", email);
    public static Guid House(string name) => For("house", name);
    public static Guid PreparedMeal(string name) => For("prepared-meal", name);
    public static Guid ShoppingItem(string ingredientName) => For("shopping-item", ingredientName);
    public static Guid PantryItem(string ingredientName) => For("pantry-item", ingredientName);
    public static Guid MealPlanItem(string key) => For("meal-plan-item", key);
    public static Guid RecipeIngredient(string key) => For("recipe-ingredient", key);
}
