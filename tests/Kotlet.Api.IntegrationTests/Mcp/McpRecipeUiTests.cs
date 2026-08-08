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
/// Exercises the MCP Apps (SEP-1865) recipe UI proof of concept: the show_recipes tool
/// advertises the ui://kotlet/recipes-v2 resource, serves structured card data with a plain
/// text fallback, and the resource itself is served as an MCP App HTML document.
/// </summary>
public sealed class McpRecipeUiTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task ToolsList_AdvertisesShowRecipesWithUiResourceMetadata()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "tools/list", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"show_recipes\"", body);
        var showRecipes = body[body.IndexOf("\"show_recipes\"", StringComparison.Ordinal)..];
        Assert.Contains("ui://kotlet/recipes-v2", showRecipes);
        Assert.Contains("resourceUri", showRecipes);
        // ChatGPT's Apps SDK links the tool to its widget via this key.
        Assert.Contains("openai/outputTemplate", showRecipes);
        Assert.Contains("outputSchema", showRecipes);
        Assert.Contains("\"recipes\"", showRecipes);
        Assert.Contains("openai/toolInvocation/invoking", showRecipes);
        Assert.Contains("Loading recipes...", showRecipes);
        Assert.Contains("openai/toolInvocation/invoked", showRecipes);
        Assert.Contains("Recipes ready", showRecipes);
        Assert.Contains("\"preview_meal_plan\"", body);
        var preview = body[body.IndexOf("\"preview_meal_plan\"", StringComparison.Ordinal)..];
        Assert.Contains("ui://kotlet/meal-plan-preview-v1", preview);
        Assert.Contains("openai/outputTemplate", preview);
        Assert.Contains("Draft meal plan ready", preview);
    }

    [Fact]
    public async Task ResourcesList_ExposesRecipeUiWithCspMetadata()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "resources/list", new { });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ui://kotlet/recipes-v2", body);
        Assert.Contains("text/html;profile=mcp-app", body);
        // Hosts enforce the iframe CSP from the full connect/resource/frame domain shape.
        Assert.Contains("connectDomains", body);
        Assert.Contains("resourceDomains", body);
        Assert.Contains("frameDomains", body);
        Assert.Contains("\"domain\":", body);
        // ChatGPT's Apps SDK reads its own snake_case CSP/domain namespace.
        Assert.Contains("openai/widgetCSP", body);
        Assert.Contains("resource_domains", body);
        Assert.Contains("openai/widgetDomain", body);
        Assert.Contains("openai/widgetDescription", body);
        Assert.Contains("prefersBorder", body);
        Assert.Contains("openai/widgetPrefersBorder", body);
        Assert.Contains("ui://kotlet/meal-plan-preview-v1", body);

        var dataLine = body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("data: ", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(dataLine["data: ".Length..]);
        var resource = document.RootElement.GetProperty("result").GetProperty("resources")
            .EnumerateArray()
            .Single(item => item.GetProperty("uri").GetString() == "ui://kotlet/recipes-v2");
        var ui = resource.GetProperty("_meta").GetProperty("ui");
        Assert.Equal("http://localhost", ui.GetProperty("domain").GetString());
        Assert.Equal(
            ["http://localhost"],
            ui.GetProperty("csp").GetProperty("resourceDomains")
                .EnumerateArray().Select(domain => domain.GetString()));
    }

    [Fact]
    public async Task RecipeUiResource_IsServedAsSelfContainedMcpAppHtml()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "resources/read", new { uri = "ui://kotlet/recipes-v2" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("text/html;profile=mcp-app", body);
        // Card grid, the MCP Apps bridge handshake, and the detail-view tool call all ship inline.
        Assert.Contains("recipe-grid", body);
        Assert.Contains("ui/initialize", body);
        // Hosts reject the ui/initialize handshake unless it carries app identity and a protocol version.
        Assert.Contains("appInfo", body);
        Assert.DoesNotContain("clientInfo", body);
        Assert.Contains("protocolVersion", body);
        Assert.Contains("get_recipe", body);
        Assert.Contains("data.totalCount === 1", body);
        Assert.Contains("data.recipes.length === 1", body);
        Assert.Contains("openRecipe(data.recipes[0].id)", body);
        Assert.Contains("attachImageFallback", body);
        Assert.DoesNotContain("onerror=", body);
        // The UI must stay self-contained: no external scripts, styles, or REST calls.
        Assert.DoesNotContain("src=\\\"http", body);
        Assert.DoesNotContain("fetch(", body);
    }

    [Fact]
    public async Task MealPlanUiResource_IsSelfContainedAndConfirmsBeforeSaving()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(
            client, accessToken, "resources/read", new { uri = "ui://kotlet/meal-plan-preview-v1" });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("text/html;profile=mcp-app", body);
        Assert.Contains("Ingredients reused", body);
        Assert.Contains("Add to Kotlet", body);
        Assert.Contains("add_weekly_meal_plan", body);
        Assert.Contains("ui/initialize", body);
        Assert.Contains("appInfo", body);
        Assert.DoesNotContain("fetch(", body);
        Assert.DoesNotContain("src=\\\"http", body);
    }

    [Fact]
    public async Task ShowRecipes_ReturnsStructuredCardsAndTextFallback()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var ingredient = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new { name = $"Chickpeas {Guid.NewGuid():N}", measurementUnit = "g", caloriesPer100BaseUnits = 164 }
        });
        var ingredientId = ExtractGuidAfter(await ingredient.Content.ReadAsStringAsync(), "\"id\":\"");
        var title = $"Chickpea stew {Guid.NewGuid():N}";
        var recipe = await CallTool(client, accessToken, "add_recipe", new
        {
            request = new
            {
                title,
                servings = 3,
                mealType = "dinner",
                descriptionMarkdown = "1. Simmer the chickpeas.",
                ingredients = new[] { new { ingredientId, quantity = 240, unit = "g" } }
            }
        });
        var recipeId = ExtractGuidAfter(await recipe.Content.ReadAsStringAsync(), "\"id\":\"");
        using var image = new ByteArrayContent(TestImages.Png());
        image.Headers.ContentType = new("image/png");
        using var upload = new MultipartFormDataContent { { image, "file", "recipe.png" } };
        (await client.PostAsync($"/api/recipes/{recipeId}/images", upload)).EnsureSuccessStatusCode();

        var response = await CallTool(client, accessToken, "show_recipes", new { search = title });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(title, body);
        // Structured content feeds the embedded UI…
        Assert.Contains("structuredContent", body);
        Assert.Contains("\"apiOrigin\"", body);
        Assert.Contains("\"ingredientCount\":1", body);
        Assert.Contains($"\"imageUrl\":\"http://localhost/api/recipes/{recipeId}/images/", body);
        // …while hosts without MCP Apps support still get a readable text list.
        Assert.Contains("Household recipes", body);
        Assert.Contains("3 serving(s)", body);
    }

    [Fact]
    public async Task RecipeTools_ExposeCleanPresentationDataAndOpenSingleShowResult()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var ingredient = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new { name = $"Lentils {Guid.NewGuid():N}", measurementUnit = "g", caloriesPer100BaseUnits = 116 }
        });
        var ingredientId = ExtractGuidAfter(await ingredient.Content.ReadAsStringAsync(), "\"id\":\"");
        var title = $"Lentil bowl {Guid.NewGuid():N}";
        var recipe = await CallTool(client, accessToken, "add_recipe", new
        {
            request = new
            {
                title,
                servings = 2,
                mealType = "dinner",
                descriptionMarkdown = "A warm lentil bowl.\n\n1. Simmer the lentils.",
                ingredients = new[] { new { ingredientId, quantity = 180, unit = "g" } }
            }
        });
        var recipeId = ExtractGuidAfter(await recipe.Content.ReadAsStringAsync(), "\"id\":\"");

        var detailResponse = await CallTool(client, accessToken, "get_recipe", new { recipeId });
        var detailResult = ReadSseResult(await detailResponse.Content.ReadAsStringAsync());
        var detailPresentation = detailResult.GetProperty("_meta").GetProperty("kotlet/recipeUi");
        Assert.Equal("detail", detailPresentation.GetProperty("kind").GetString());
        var detail = detailPresentation.GetProperty("detail");
        Assert.Equal(title, detail.GetProperty("title").GetString());
        Assert.Equal("A warm lentil bowl.\n\n1. Simmer the lentils.", detail.GetProperty("description").GetString());
        Assert.False(detail.GetProperty("isIncomplete").GetBoolean());
        Assert.True(detail.GetProperty("canEdit").GetBoolean());
        Assert.True(detail.TryGetProperty("editUrl", out var editUrl), "Editable details should expose an edit URL.");
        Assert.Equal($"http://localhost:4200/recipes/{recipeId}/edit", editUrl.GetString());
        AssertDoesNotContainKey(detail, "createdAtUtc");
        AssertDoesNotContainKey(detail, "updatedAtUtc");
        AssertDoesNotContainKey(detail, "createdByUserId");
        AssertDoesNotContainKey(detail, "slug");
        AssertDoesNotContainKey(detail, "sourceUrl");
        AssertDoesNotContainKey(detail, "isAiAssisted");
        AssertDoesNotContainKey(detail, "preparationTimeMinutes");
        AssertDoesNotContainKey(detail, "cookingTimeMinutes");
        AssertDoesNotContainKey(detail, "totalTimeMinutes");

        var searchResponse = await CallTool(client, accessToken, "get_recipes", new { search = title });
        var searchResult = ReadSseResult(await searchResponse.Content.ReadAsStringAsync());
        var searchPresentation = searchResult.GetProperty("_meta").GetProperty("kotlet/recipeUi");
        Assert.Equal("list", searchPresentation.GetProperty("kind").GetString());
        var card = Assert.Single(searchPresentation.GetProperty("recipes").EnumerateArray());
        Assert.Equal(title, card.GetProperty("title").GetString());
        Assert.Equal("A warm lentil bowl.", card.GetProperty("description").GetString());
        AssertDoesNotContainKey(card, "resourceUri");
        AssertDoesNotContainKey(card, "updatedAtUtc");
        AssertDoesNotContainKey(card, "isAiAssisted");

        var showResponse = await CallTool(client, accessToken, "show_recipes", new { search = title });
        var showResult = ReadSseResult(await showResponse.Content.ReadAsStringAsync());
        var showPresentation = showResult.GetProperty("_meta").GetProperty("kotlet/recipeUi");
        Assert.Equal("list", showPresentation.GetProperty("kind").GetString());
        Assert.Equal(title, showPresentation.GetProperty("detail").GetProperty("title").GetString());
    }

    [Fact]
    public async Task RecipeDetailPresentation_MarksIncompleteRecipesWithoutRawFields()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var title = $"Incomplete recipe {Guid.NewGuid():N}";
        var recipe = await CallTool(client, accessToken, "add_recipe", new
        {
            request = new { title, servings = 1, ingredients = Array.Empty<object>() }
        });
        var recipeId = ExtractGuidAfter(await recipe.Content.ReadAsStringAsync(), "\"id\":\"");

        var response = await CallTool(client, accessToken, "get_recipe", new { recipeId });
        var result = ReadSseResult(await response.Content.ReadAsStringAsync());
        var detail = result.GetProperty("_meta").GetProperty("kotlet/recipeUi").GetProperty("detail");
        Assert.True(detail.GetProperty("isIncomplete").GetBoolean());
        Assert.True(detail.GetProperty("canEdit").GetBoolean());
        Assert.Equal(title, detail.GetProperty("title").GetString());
        Assert.Empty(detail.GetProperty("ingredients").EnumerateArray());
        AssertDoesNotContainKey(detail, "createdAtUtc");
    }

    [Fact]
    public async Task ToolsList_AttachesAnUiResourceToEveryTool()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "tools/list", new { });
        var body = await response.Content.ReadAsStringAsync();
        var dataLine = body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("data: ", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(dataLine["data: ".Length..]);
        var tools = document.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

        Assert.NotEmpty(tools);
        Assert.All(tools, tool =>
        {
            var meta = tool.GetProperty("_meta");
            Assert.True(meta.TryGetProperty("openai/outputTemplate", out var template), tool.GetProperty("name").GetString());
            Assert.StartsWith("ui://kotlet/", template.GetString(), StringComparison.Ordinal);
            Assert.True(meta.GetProperty("ui").TryGetProperty("resourceUri", out _));
        });
        // Dedicated MCP Apps tools keep their own widget resources; all remaining tools fall back
        // to the shared data renderer. Recipe search and detail use the recipe widget too.
        string[] dedicatedUiTools = ["show_recipes", "show_meal_plan", "preview_meal_plan", "get_recipes", "get_recipe"];
        Assert.All(tools.Where(tool => !dedicatedUiTools.Contains(tool.GetProperty("name").GetString())), tool =>
            Assert.Equal("ui://kotlet/data-v3", tool.GetProperty("_meta").GetProperty("openai/outputTemplate").GetString()));
        Assert.All(tools.Where(tool => tool.GetProperty("name").GetString() is "get_recipes" or "get_recipe"), tool =>
            Assert.Equal("ui://kotlet/recipes-v2", tool.GetProperty("_meta").GetProperty("openai/outputTemplate").GetString()));
    }

    [Fact]
    public async Task SharedDataUiResource_IsSelfContainedAndUsesReusableResultComponents()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "resources/read", new { uri = "ui://kotlet/data-v3" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("text/html;profile=mcp-app", body);
        Assert.Contains("ui/initialize", body);
        Assert.Contains("ui/notifications/tool-result", body);
        Assert.Contains("grid-template-columns", body);
        Assert.Contains("function table", body);
        Assert.Contains("function shopping", body);
        Assert.Contains("function pantry", body);
        Assert.Contains("function prepared", body);
        Assert.Contains("function mealPlan", body);
        Assert.Contains("function ingredientMatches", body);
        Assert.Contains("function duplicates", body);
        Assert.Contains(".tag{", body);
        Assert.DoesNotContain("src=\"http", body);
        Assert.DoesNotContain("fetch(", body);
    }

    [Fact]
    public async Task CompactRecipeSearch_AndMealPlanPreview_SupportOneRequestFromDraftToSave()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var ingredientName = $"Tomato {Guid.NewGuid():N}";
        var ingredient = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new { name = ingredientName, measurementUnit = "g", caloriesPer100BaseUnits = 18 }
        });
        var ingredientId = ExtractGuidAfter(await ingredient.Content.ReadAsStringAsync(), "\"id\":\"");
        var title = $"Fast tomato soup {Guid.NewGuid():N}";
        var recipe = await CallTool(client, accessToken, "add_recipe", new
        {
            request = new
            {
                title,
                servings = 4,
                mealType = "dinner",
                descriptionMarkdown = "METHOD_SHOULD_ONLY_APPEAR_IN_DETAIL",
                ingredients = new[] { new { ingredientId, quantity = 500, unit = "g" } }
            }
        });
        var recipeId = ExtractGuidAfter(await recipe.Content.ReadAsStringAsync(), "\"id\":\"");

        var candidates = await CallTool(client, accessToken, "get_recipes", new
        {
            ingredientIds = new[] { ingredientId },
            mealType = "dinner",
            pageSize = 5
        });
        var candidatesBody = await candidates.Content.ReadAsStringAsync();
        Assert.Contains(title, candidatesBody);
        Assert.Contains(ingredientName, candidatesBody);
        Assert.Contains($"kotlet://recipes/{recipeId}", candidatesBody);
        var candidateResult = ReadSseResult(candidatesBody);
        Assert.DoesNotContain(
            "METHOD_SHOULD_ONLY_APPEAR_IN_DETAIL",
            candidateResult.GetProperty("structuredContent").GetRawText());

        var request = new
        {
            weekStart = "2027-02-01",
            meals = new[]
            {
                new { date = "2027-02-01", slot = "dinner", recipeId },
                new { date = "2027-02-03", slot = "dinner", recipeId }
            }
        };
        var preview = await CallTool(client, accessToken, "preview_meal_plan", new { request });
        var previewBody = await preview.Content.ReadAsStringAsync();
        Assert.Contains("structuredContent", previewBody);
        Assert.Contains("\"saveRequest\"", previewBody);
        Assert.Contains(title, previewBody);
        Assert.Contains(ingredientName, previewBody);
        Assert.Contains("not saved", previewBody);

        var beforeSave = await CallTool(
            client, accessToken, "get_meal_plan", new { from = "2027-02-01", days = 3 });
        Assert.DoesNotContain(title, await beforeSave.Content.ReadAsStringAsync());

        var saved = await CallTool(client, accessToken, "add_weekly_meal_plan", new { request });
        var savedBody = await saved.Content.ReadAsStringAsync();
        Assert.Contains("\"Success\"", savedBody);
        Assert.Contains(title, savedBody);
        var afterSave = await CallTool(
            client, accessToken, "get_meal_plan", new { from = "2027-02-01", days = 3 });
        var afterSaveBody = await afterSave.Content.ReadAsStringAsync();
        Assert.Contains("2027-02-01", afterSaveBody);
        Assert.Contains("2027-02-03", afterSaveBody);
        Assert.Contains(title, afterSaveBody);
    }

    [Fact]
    public async Task ToolResults_ReportTheNegotiatedLanguageSoTheUiCanLocalizeItself()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var polish = await CallTool(client, accessToken, "get_pantry", new { }, "pl-PL,pl;q=0.9,en;q=0.5");
        Assert.Contains("\"kotlet/locale\":\"pl\"", await polish.Content.ReadAsStringAsync());

        var english = await CallTool(client, accessToken, "show_recipes", new { }, "en-GB");
        Assert.Contains("\"kotlet/locale\":\"en\"", await english.Content.ReadAsStringAsync());

        // Without a usable Accept-Language the server stays silent, leaving the app free to fall
        // back to the host's own UI locale instead of being pinned to the server default.
        var unspecified = await CallTool(client, accessToken, "get_pantry", new { });
        Assert.DoesNotContain("kotlet/locale", await unspecified.Content.ReadAsStringAsync());

        var unsupported = await CallTool(client, accessToken, "get_pantry", new { }, "fr-FR");
        Assert.DoesNotContain("kotlet/locale", await unsupported.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UiResources_ShipEverySupportedLanguageInline()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        foreach (var (uri, english, polish) in new[]
                 {
                     ("ui://kotlet/data-v3", "Shopping list", "Lista zakupów"),
                     ("ui://kotlet/recipes-v2", "View recipe", "Zobacz przepis"),
                     ("ui://kotlet/meal-plan-v1", "Read-only", "Tylko do odczytu"),
                     ("ui://kotlet/meal-plan-preview-v1", "Add to Kotlet", "Dodaj do Kotleta")
                 })
        {
            var response = await SendMcp(client, accessToken, "resources/read", new { uri });
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains(english, body);
            // JSON-encoded in the SSE payload, so compare against the escaped form the client sees.
            Assert.Contains(JsonEncodedText.Encode(polish).ToString(), body);
            Assert.Contains("kotlet/locale", body);
        }
    }

    /// <summary>Registers a user with a home and runs the OAuth PKCE flow for an MCP-scoped token.</summary>
    private async Task<(HttpClient Client, string AccessToken)> AuthorizeMcpClientAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"mcp-ui-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        registration.EnsureSuccessStatusCode();
        var registrationToken = (await registration.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registrationToken);
        var house = await client.PostAsJsonAsync("/api/houses", new { name = "MCP UI home" });
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

    private static JsonElement ReadSseResult(string body)
    {
        var dataLine = body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("data:", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(dataLine[5..].Trim());
        return document.RootElement.GetProperty("result").Clone();
    }

    private static void AssertDoesNotContainKey(JsonElement value, string key) =>
        Assert.False(value.TryGetProperty(key, out _),
            $"'{key}' must not be present in the recipe presentation payload.");

    private static Task<HttpResponseMessage> CallTool(
        HttpClient client, string accessToken, string name, object arguments, string? language = null)
        => SendMcp(client, accessToken, "tools/call", new { name, arguments }, language);

    private static Task<HttpResponseMessage> SendMcp(
        HttpClient client, string accessToken, string method, object parameters, string? language = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        if (language is not null)
            request.Headers.AcceptLanguage.ParseAdd(language);
        request.Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters });
        return client.SendAsync(request);
    }
}
