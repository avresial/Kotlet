using System.Diagnostics;
using System.Text.Json;
using Kotlet.TestData;

namespace Kotlet.Bench;

/// <summary>
/// The fixed workload every run measures. The data comes from <see cref="KotletTestData"/>, so
/// the fixture is shared with the integration and browser tests rather than invented here.
/// Changing the call list below, or the fixture it reads, invalidates existing baselines.
/// </summary>
public sealed class McpScenario(McpSession session, DbQueryCounter? counter)
{
    public static string FixtureDescription =>
        $"{KotletTestData.RecipeCount} recipes, {KotletTestData.PlannedDays} planned days x " +
        $"{KotletTestData.PlannedSlots.Length} slots, {KotletTestData.ShoppingItemCount} shopping items, " +
        $"{KotletTestData.PantryItemCount} pantry items, full ingredient catalogue";

    /// <summary>
    /// Names handed to the fuzzy ingredient search: some exact catalogue entries, some
    /// misspelled, one absent. That mix is what an agent importing a recipe actually sends.
    /// </summary>
    private static readonly string[] IngredientQuery =
    [
        "Potato", "Carrot", "Onion", "Butter", "Chicken breast",
        "tomatoe", "olif oil", "smoked paprica"
    ];

    private static string PlanStart => KotletTestData.PlanStart.ToString("yyyy-MM-dd");

    /// <summary>The read-only calls an agent makes most often, timed over several repeats.</summary>
    public IReadOnlyList<(string Label, string Tool, object Arguments)> ReadCalls() =>
    [
        ("get_recipes", "get_recipes", new { }),
        ("get_recipe (one)", "get_recipe",
            new { recipeId = TestIds.Recipe(KotletTestData.RecipeTitle(0)) }),
        ("get_ingredients (x8)", "get_ingredients", new { names = IngredientQuery }),
        ("get_shopping_list", "get_shopping_list", new { }),
        ("get_pantry", "get_pantry", new { }),
        ("get_prepared_meals", "get_prepared_meals", new { }),
        ("get_meal_plan_overview (7d)", "get_meal_plan_overview", new { from = PlanStart, days = 7 }),
        ("get_meal_plan (7d)", "get_meal_plan", new { from = PlanStart, days = 7 }),
        ("get_meal_plan (31d)", "get_meal_plan", new { from = PlanStart, days = 31 }),
        ("show_recipes", "show_recipes", new { })
    ];

    /// <summary>
    /// A whole "import a recipe I found online" workflow, the way the server's own instructions
    /// tell an agent to do it. Each call here is one model turn in a real conversation, which is
    /// why the round-trip count matters more than any single call's duration.
    /// </summary>
    public async Task<SessionResult> MeasureRecipeImportAsync()
    {
        var queriesBefore = counter?.Count ?? 0;
        var stopwatch = Stopwatch.StartNew();
        var bytes = 0;
        var roundTrips = 0;

        async Task<McpCallResult> Step(Func<Task<McpCallResult>> call)
        {
            var result = await call();
            bytes += result.WireBytes;
            roundTrips++;
            return result;
        }

        await Step(() => session.SendAsync("resources/read", new { uri = "kotlet://recipes/new-recipe-guide" }));

        await Step(() => session.CallToolAsync("check_recipe_exists",
            new { title = "Imported bench dish", sourceUrl = "https://example.com/bench-dish" }));

        await Step(() => session.CallToolAsync("get_ingredients",
            new { names = new[] { "Potato", "Carrot", "Smoked bench paprika" } }));

        var missing = await Step(() => session.CallToolAsync("create_ingredient", new
        {
            request = new
            {
                name = "Smoked bench paprika",
                measurementUnit = "g",
                caloriesPer100BaseUnits = 280m,
                category = "Spice"
            }
        }));

        await Step(() => session.CallToolAsync("add_recipe", new
        {
            request = new
            {
                title = "Imported bench dish",
                servings = 4,
                descriptionMarkdown = "1. Season everything.\n2. Roast until done.",
                sourceUrl = "https://example.com/bench-dish",
                isAiAssisted = true,
                ingredients = new[]
                {
                    new { ingredientId = TestIds.Ingredient("Potato").ToString(), quantity = 200m, unit = "g" },
                    new { ingredientId = TestIds.Ingredient("Carrot").ToString(), quantity = 150m, unit = "g" },
                    new { ingredientId = CreatedIngredientId(missing), quantity = 10m, unit = "g" }
                }
            }
        }));

        stopwatch.Stop();
        return new SessionResult(
            "Import a recipe found online",
            roundTrips,
            stopwatch.Elapsed.TotalMilliseconds,
            bytes,
            counter is null ? null : counter.Count - queriesBefore);
    }

    private static string CreatedIngredientId(McpCallResult result)
    {
        var structured = result.Result.GetProperty("structuredContent");
        if (structured.TryGetProperty("ingredient", out var ingredient) && ingredient.ValueKind == JsonValueKind.Object)
            return ingredient.GetProperty("id").GetString()!;
        throw new InvalidOperationException(
            $"Expected create_ingredient to return an ingredient but got: {structured.GetRawText()}");
    }
}
