using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Mcp;

/// <summary>
/// Exercises the MCP tool surface an AI agent uses to browse household data and to import a
/// recipe found on the internet (create missing ingredients, then add the recipe one-shot).
/// </summary>
public sealed class McpDataBrowsingTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task ToolsList_ExposesBrowseAndRecipeImportTools()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "tools/list", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        foreach (var tool in new[]
                 {
                     "get_recipes", "get_recipe", "get_ingredients", "get_ingredient",
                     "get_shopping_list", "get_pantry", "get_meal_plan_overview", "get_meal_plan",
                     "add_recipe", "create_ingredient",
                     "add_pantry_item", "update_pantry_item", "remove_pantry_item"
                 })
            Assert.Contains($"\"{tool}\"", body);
    }

    [Fact]
    public async Task RecipeImportFlow_CreatesMissingIngredient_ThenRecipe_AndBrowsesIt()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        // The agent found a recipe online with an ingredient Kotlet does not know yet.
        var ingredientName = $"Smoked paprika {Guid.NewGuid():N}";
        var created = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new
            {
                name = ingredientName,
                measurementUnit = "g",
                caloriesPer100BaseUnits = 282,
                category = "Spice",
                attributes = new[] { "PlantOrigin", "Smoked" },
                suitability = new[] { "Vegan", "Vegetarian", "GlutenFree" }
            }
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.Contains("\"Success\"", createdBody);
        Assert.Contains(ingredientName, createdBody);
        var ingredientId = ExtractGuidAfter(createdBody, "\"id\":\"");

        // Full ingredient details come back with readable category/attribute names, not bitmasks.
        var ingredient = await CallTool(client, accessToken, "get_ingredient", new { ingredientId });
        var ingredientBody = await ingredient.Content.ReadAsStringAsync();
        Assert.Contains("\"Spice\"", ingredientBody);
        Assert.Contains("\"Smoked\"", ingredientBody);
        Assert.Contains("\"Vegan\"", ingredientBody);

        var recipe = await CallTool(client, accessToken, "add_recipe", new
        {
            request = new
            {
                title = $"Imported goulash {Guid.NewGuid():N}",
                servings = 4,
                descriptionMarkdown = "Rich goulash.\n\n1. Brown the meat.\n2. Add paprika and simmer.\n\nSource: https://example.com/goulash",
                ingredients = new[] { new { ingredientId, quantity = 15, unit = "g", note = "sweet variety" } }
            }
        });
        Assert.Equal(HttpStatusCode.OK, recipe.StatusCode);
        var recipeBody = await recipe.Content.ReadAsStringAsync();
        Assert.Contains("\"Success\"", recipeBody);
        var recipeId = ExtractGuidAfter(recipeBody, "\"id\":\"");

        var detail = await CallTool(client, accessToken, "get_recipe", new { recipeId });
        var detailBody = await detail.Content.ReadAsStringAsync();
        Assert.Contains("Source: https://example.com/goulash", detailBody);
        Assert.Contains("sweet variety", detailBody);
    }

    [Fact]
    public async Task CreateIngredient_WithUnknownEnumNames_ReturnsActionableValidationErrors()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new
            {
                name = $"Bad enum ingredient {Guid.NewGuid():N}",
                measurementUnit = "g",
                caloriesPer100BaseUnits = 10,
                category = "Snacks",
                allergens = new[] { "Gluten", "Kryptonite" }
            }
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ValidationFailed", body);
        Assert.Contains("Snacks", body);
        Assert.Contains("Kryptonite", body);
    }

    [Fact]
    public async Task PantryAndShoppingList_AreBrowsableAndEditableThroughTools()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var ingredientName = $"Oat milk {Guid.NewGuid():N}";
        var created = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new { name = ingredientName, measurementUnit = "ml", caloriesPer100BaseUnits = 45 }
        });
        var ingredientId = ExtractGuidAfter(await created.Content.ReadAsStringAsync(), "\"id\":\"");

        var added = await CallTool(client, accessToken, "add_pantry_item", new
        {
            ingredientId, quantity = 1000, storageLocation = "Refrigerator", expirationDate = "2027-01-15"
        });
        var addedBody = await added.Content.ReadAsStringAsync();
        Assert.Contains("\"Success\"", addedBody);
        Assert.Contains("Refrigerator", addedBody);
        var pantryItemId = ExtractGuidAfter(addedBody, "\"id\":\"");

        var pantry = await CallTool(client, accessToken, "get_pantry", new { });
        var pantryBody = await pantry.Content.ReadAsStringAsync();
        Assert.Contains(ingredientName, pantryBody);
        Assert.Contains("2027-01-15", pantryBody);

        var pantryResource = await SendMcp(client, accessToken, "resources/read", new { uri = "kotlet://pantry" });
        Assert.Contains(ingredientName, await pantryResource.Content.ReadAsStringAsync());

        var updated = await CallTool(client, accessToken, "update_pantry_item", new { itemId = pantryItemId, quantity = 250 });
        Assert.Contains("250", await updated.Content.ReadAsStringAsync());

        var removed = await CallTool(client, accessToken, "remove_pantry_item", new { itemId = pantryItemId });
        Assert.Contains("true", (await removed.Content.ReadAsStringAsync()).ToLowerInvariant());

        var invalidLocation = await CallTool(client, accessToken, "add_pantry_item", new
        {
            ingredientId, quantity = 10, storageLocation = "Garage"
        });
        var invalidBody = await invalidLocation.Content.ReadAsStringAsync();
        Assert.Contains("ValidationFailed", invalidBody);
        Assert.Contains("Refrigerator, Freezer, Cabinet", invalidBody);

        await CallTool(client, accessToken, "add_shopping_list_item", new
        {
            request = new { ingredientId, quantity = 500 }
        });
        var shoppingList = await CallTool(client, accessToken, "get_shopping_list", new { });
        Assert.Contains(ingredientName, await shoppingList.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetMealPlan_ReturnsOneFullDayPerRequestedDate()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await CallTool(client, accessToken, "get_meal_plan", new { from = "2026-07-06", days = 3 });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("2026-07-06", body);
        Assert.Contains("2026-07-08", body);
        Assert.DoesNotContain("2026-07-09", body);

        var invalid = await CallTool(client, accessToken, "get_meal_plan", new { from = "06.07.2026", days = 3 });
        Assert.Contains("yyyy-MM-dd", await invalid.Content.ReadAsStringAsync());
    }

    /// <summary>Registers a user with a home and runs the OAuth PKCE flow for an MCP-scoped token.</summary>
    private async Task<(HttpClient Client, string AccessToken)> AuthorizeMcpClientAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"mcp-browse-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password = "Password1!", confirmPassword = "Password1!"
        });
        registration.EnsureSuccessStatusCode();
        var registrationToken = (await registration.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registrationToken);
        var house = await client.PostAsJsonAsync("/api/houses", new { name = "MCP browse home" });
        house.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await house.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetProperty("accessToken").GetString());

        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorization = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "kotlet-mcp-tests",
            ["response_type"] = "code",
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["scope"] = "mcp",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = "http://localhost/mcp"
        });
        var authorizeResponse = await client.GetAsync(authorization);
        Assert.Equal(HttpStatusCode.Redirect, authorizeResponse.StatusCode);
        var code = Assert.Single(QueryHelpers.ParseQuery(authorizeResponse.Headers.Location!.Query)["code"]);
        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "kotlet-mcp-tests",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["code_verifier"] = verifier,
            ["resource"] = "http://localhost/mcp"
        }));
        tokenResponse.EnsureSuccessStatusCode();
        var accessToken = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;
        return (client, accessToken);
    }

    private static Guid ExtractGuidAfter(string body, string marker)
    {
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Marker '{marker}' not found in: {body}");
        start += marker.Length;
        return Guid.Parse(body.Substring(start, 36));
    }

    private static Task<HttpResponseMessage> CallTool(HttpClient client, string accessToken, string name, object arguments)
        => SendMcp(client, accessToken, "tools/call", new { name, arguments });

    private static Task<HttpResponseMessage> SendMcp(
        HttpClient client, string accessToken, string method, object parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        request.Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters });
        return client.SendAsync(request);
    }
}
