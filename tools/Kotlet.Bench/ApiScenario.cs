using System.Diagnostics;
using Kotlet.TestData;

namespace Kotlet.Bench;

/// <summary>
/// The REST endpoints the Angular app hits to paint a screen. These are the calls a user waits
/// on, so the fixed list below is the read path of each main page rather than a sweep of the
/// whole API — writes are excluded so a run can repeat without changing the fixture.
/// </summary>
public sealed class ApiScenario(HttpClient client, DbQueryCounter? counter)
{
    private static string PlanStart => KotletTestData.PlanStart.ToString("yyyy-MM-dd");

    /// <summary>
    /// Label, screen it serves, and path. The label is the diff key, so renaming one detaches
    /// it from every earlier baseline.
    /// </summary>
    public static IReadOnlyList<(string Label, string Screen, string Path)> Calls() =>
    [
        ("auth/me", "every page", "/api/auth/me"),
        ("houses", "every page", "/api/houses"),
        ("dashboard/stats", "dashboard", "/api/dashboard/stats"),
        ("recipes/recent", "dashboard", "/api/recipes/recent?limit=4"),
        ("recipes (page 1)", "recipe list", "/api/recipes?page=1&pageSize=20"),
        ("recipes/{id}", "recipe detail", $"/api/recipes/{TestIds.Recipe(KotletTestData.RecipeTitle(0))}"),
        ("ingredients (all)", "recipe editor", "/api/ingredients"),
        ("meal-planner/overview (28d)", "meal planner", $"/api/meal-planner/overview?from={PlanStart}&days=28"),
        ("meal-planner (one day)", "meal planner", $"/api/meal-planner?date={PlanStart}"),
        ("meal-planner/members", "meal planner", "/api/meal-planner/members"),
        ("shopping-list", "shopping list", "/api/shopping-list"),
        ("pantry", "pantry", "/api/pantry"),
        ("pantry/recipe-matches", "pantry", "/api/pantry/recipe-matches"),
        // includeArchived has no default on the endpoint, so omitting it is a 400. The frontend
        // always sends it; the benchmark matches that rather than measuring an error page.
        ("prepared-meals", "prepared meals", "/api/prepared-meals?includeArchived=false")
    ];

    /// <summary>Issues one GET and reports what the wire, the clock, and the database saw.</summary>
    public async Task<(int StatusCode, double ElapsedMs, int Bytes, int? Queries)> GetAsync(string path)
    {
        var queriesBefore = counter?.Count ?? 0;
        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsByteArrayAsync();
        stopwatch.Stop();

        return (
            (int)response.StatusCode,
            stopwatch.Elapsed.TotalMilliseconds,
            body.Length,
            counter is null ? null : counter.Count - queriesBefore);
    }
}
