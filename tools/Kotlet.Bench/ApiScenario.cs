using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kotlet.Bench;

/// <summary>
/// The REST endpoints the Angular app hits to paint a screen. These are the calls a user waits
/// on, so the fixed list below is the read path of each main page rather than a sweep of the
/// whole API — writes are excluded so a run can repeat without changing the fixture.
/// </summary>
public sealed class ApiScenario(HttpClient client, DbQueryCounter? counter, DateOnly planStart)
{
    private readonly string from = planStart.ToString("yyyy-MM-dd");

    /// <summary>
    /// Label, screen it serves, and path. The label is the diff key, so renaming one detaches
    /// it from every earlier baseline.
    /// </summary>
    /// <remarks>
    /// The recipe-detail path needs a real id, so it is resolved from the recipe list rather
    /// than hard-coded to a fixture id: hard-coding works in process and 404s against a real
    /// deployment, which would have the benchmark timing an error page.
    /// </remarks>
    public async Task<IReadOnlyList<(string Label, string Screen, string Path)>> CallsAsync(List<string> faults)
    {
        var calls = new List<(string, string, string)>
        {
            ("auth/me", "every page", "/api/auth/me"),
            ("houses", "every page", "/api/houses"),
            ("dashboard/stats", "dashboard", "/api/dashboard/stats"),
            ("recipes/recent", "dashboard", "/api/recipes/recent?limit=4"),
            ("recipes (page 1)", "recipe list", "/api/recipes?page=1&pageSize=20")
        };

        if (await FirstRecipeIdAsync(faults) is { } recipeId)
            calls.Add(("recipes/{id}", "recipe detail", $"/api/recipes/{recipeId}"));

        calls.AddRange(
        [
            ("ingredients (all)", "recipe editor", "/api/ingredients"),
            ("meal-planner/overview (28d)", "meal planner", $"/api/meal-planner/overview?from={from}&days=28"),
            ("meal-planner (one day)", "meal planner", $"/api/meal-planner?date={from}"),
            ("meal-planner/members", "meal planner", "/api/meal-planner/members"),
            ("shopping-list", "shopping list", "/api/shopping-list"),
            ("pantry", "pantry", "/api/pantry"),
            ("pantry/recipe-matches", "pantry", "/api/pantry/recipe-matches"),
            // includeArchived has no default on the endpoint, so omitting it is a 400. The
            // frontend always sends it; the benchmark matches that rather than measuring an
            // error page.
            ("prepared-meals", "prepared meals", "/api/prepared-meals?includeArchived=false")
        ]);

        return calls;
    }

    /// <summary>
    /// The first recipe the list endpoint returns, or null when the household has none. The
    /// list order is deterministic, so in-process runs always resolve the same recipe.
    /// </summary>
    /// <remarks>
    /// This request plans the call list rather than being measured, but it can still fail — and
    /// a failure here has to reach the run's fault list rather than throwing, or the process
    /// dies with a stack trace instead of the reported exit-3 path.
    /// </remarks>
    private async Task<string?> FirstRecipeIdAsync(List<string> faults)
    {
        const string path = "/api/recipes?page=1&pageSize=1";
        var response = await client.GetAsync(path);
        if (!response.IsSuccessStatusCode)
        {
            faults.Add($"GET {path} (resolving a recipe id) returned HTTP {(int)response.StatusCode}");
            return null;
        }

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        return page.TryGetProperty("items", out var items) && items.GetArrayLength() > 0
            ? items[0].GetProperty("id").GetString()
            : null;
    }

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
