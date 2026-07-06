using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kotlet.Domain.Ingredients;
using Kotlet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Auth;

public sealed class OAuthEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task AuthorizationCodeFlow_UsesPkceAndBindsTokenToMcpResource()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var unauthorizedMcp = await client.PostAsync("/mcp", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedMcp.StatusCode);
        Assert.Contains(unauthorizedMcp.Headers.WwwAuthenticate,
            header => header.Parameter?.Contains("resource_metadata", StringComparison.Ordinal) == true);

        var email = $"oauth-{Guid.NewGuid():N}@example.com";
        var registrationResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        registrationResponse.EnsureSuccessStatusCode();
        var registrationToken = (await registrationResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registrationToken);
        var houseResponse = await client.PostAsJsonAsync("/api/houses", new { name = "OAuth home" });
        houseResponse.EnsureSuccessStatusCode();
        var houseBody = await houseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var houseId = houseBody.GetProperty("house").GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", houseBody.GetProperty("token").GetProperty("accessToken").GetString());

        var ingredientId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KotletDbContext>();
            db.Ingredients.Add(new Ingredient
            {
                Id = ingredientId,
                Name = "OAuth tomato",
                MeasurementUnit = "g"
            });
            await db.SaveChangesAsync();
        }
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            title = "OAuth tomato soup",
            servings = 2,
            ingredients = new[] { new { ingredientId, quantity = 500, unit = "g" } }
        });
        recipeResponse.EnsureSuccessStatusCode();
        var recipeId = (await recipeResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var metadata = await client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration");
        Assert.EndsWith("/connect/authorize", metadata.GetProperty("authorization_endpoint").GetString());
        Assert.EndsWith("/connect/token", metadata.GetProperty("token_endpoint").GetString());
        Assert.Contains("S256", metadata.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(item => item.GetString()));

        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorization = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "kotlet-mcp-tests",
            ["response_type"] = "code",
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["scope"] = "mcp offline_access",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = "http://localhost/mcp",
            ["state"] = "test-state"
        });

        var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRedirect = await anonymous.GetAsync(authorization);
        Assert.StartsWith("http://localhost:4200/login?returnUrl=", loginRedirect.Headers.Location!.AbsoluteUri);

        var authorizeResponse = await client.GetAsync(authorization);
        Assert.True(authorizeResponse.StatusCode == HttpStatusCode.Redirect,
            await authorizeResponse.Content.ReadAsStringAsync());
        var callback = QueryHelpers.ParseQuery(authorizeResponse.Headers.Location!.Query);
        Assert.Equal("test-state", callback["state"]);
        var code = Assert.Single(callback["code"]);

        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "kotlet-mcp-tests",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["code_verifier"] = verifier,
            ["resource"] = "http://localhost/mcp"
        }));

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = token.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains("http://localhost/mcp", jwt.Audiences);
        Assert.Equal(houseId.ToString(), jwt.Claims.Single(claim => claim.Type == "house_id").Value);
        Assert.False(string.IsNullOrWhiteSpace(token.GetProperty("refresh_token").GetString()));

        var identityResource = await ReadResource(client, accessToken!, "kotlet://identity");
        Assert.Equal(HttpStatusCode.OK, identityResource.StatusCode);
        Assert.Contains(email, await identityResource.Content.ReadAsStringAsync());

        var recipesResponse = await CallTool(client, accessToken!, "get_recipes", new { });
        Assert.Equal(HttpStatusCode.OK, recipesResponse.StatusCode);
        Assert.Contains($"kotlet://recipes/{recipeId}", await recipesResponse.Content.ReadAsStringAsync());

        var recipeResource = await ReadResource(client, accessToken!, $"kotlet://recipes/{recipeId}");
        Assert.Equal(HttpStatusCode.OK, recipeResource.StatusCode);
        Assert.Contains("OAuth tomato soup", await recipeResource.Content.ReadAsStringAsync());

        var recipeGuideResource = await ReadResource(client, accessToken!, "kotlet://recipes/new-recipe-guide");
        Assert.Equal(HttpStatusCode.OK, recipeGuideResource.StatusCode);
        var recipeGuideBody = await recipeGuideResource.Content.ReadAsStringAsync();
        Assert.Contains("add_recipe", recipeGuideBody);
        Assert.Contains("does not expose an edit recipe tool", recipeGuideBody);

        var recipePromptResponse = await GetPrompt(client, accessToken!, "create_recipe_flow");
        Assert.Equal(HttpStatusCode.OK, recipePromptResponse.StatusCode);
        var recipePromptBody = await recipePromptResponse.Content.ReadAsStringAsync();
        Assert.Contains("one-shot operation", recipePromptBody);
        Assert.Contains("add_recipe", recipePromptBody);

        var addRecipeResponse = await CallTool(client, accessToken!, "add_recipe", new
        {
            request = new
            {
                title = "OAuth roasted tomato toast",
                servings = 1,
                descriptionMarkdown = "Roasted tomato toast.\n\n1. Roast the tomatoes.\n2. Spoon tomatoes over toast and serve.",
                ingredients = new[] { new { ingredientId, quantity = 150, unit = "g", note = "roasted" } }
            }
        });
        Assert.Equal(HttpStatusCode.OK, addRecipeResponse.StatusCode);
        var addRecipeBody = await addRecipeResponse.Content.ReadAsStringAsync();
        Assert.Contains("OAuth roasted tomato toast", addRecipeBody);
        Assert.Contains("roasted", addRecipeBody);

        var mealPlanResource = await ReadResource(client, accessToken!, "kotlet://meal-plans/days/2026-06-29");
        Assert.Equal(HttpStatusCode.OK, mealPlanResource.StatusCode);
        Assert.Contains("breakfast", await mealPlanResource.Content.ReadAsStringAsync());

        var weeklyPlanResponse = await CallTool(client, accessToken!, "add_weekly_meal_plan",
            new { request = new { weekStart = "2026-06-29", meals = Array.Empty<object>() } });
        Assert.Equal(HttpStatusCode.OK, weeklyPlanResponse.StatusCode);
        Assert.Contains("added", await weeklyPlanResponse.Content.ReadAsStringAsync());

        var shoppingListResponse = await ReadResource(client, accessToken!, "kotlet://shopping-list");
        Assert.Equal(HttpStatusCode.OK, shoppingListResponse.StatusCode);
        Assert.DoesNotContain("isError\":true", await shoppingListResponse.Content.ReadAsStringAsync());

        var invalidResource = await client.GetAsync(authorization.Replace(
            Uri.EscapeDataString("http://localhost/mcp"),
            Uri.EscapeDataString("http://localhost/not-mcp"),
            StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.BadRequest, invalidResource.StatusCode);
        Assert.Contains("invalid_target", await invalidResource.Content.ReadAsStringAsync());

        var invalidRedirect = await client.GetAsync(authorization.Replace(
            Uri.EscapeDataString("http://127.0.0.1/callback"),
            Uri.EscapeDataString("http://127.0.0.1/wrong"),
            StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.BadRequest, invalidRedirect.StatusCode);

        var secondAuthorization = await client.GetAsync(authorization);
        var secondCode = Assert.Single(QueryHelpers.ParseQuery(secondAuthorization.Headers.Location!.Query)["code"]);
        var invalidVerifier = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "kotlet-mcp-tests",
            ["code"] = secondCode!,
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["code_verifier"] = verifier + "wrong",
            ["resource"] = "http://localhost/mcp"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, invalidVerifier.StatusCode);
        Assert.Equal("invalid_grant", (await invalidVerifier.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    private static Task<HttpResponseMessage> CallTool(HttpClient client, string accessToken, string name, object arguments)
        => SendMcp(client, accessToken, "tools/call", new { name, arguments });

    private static Task<HttpResponseMessage> ReadResource(HttpClient client, string accessToken, string uri)
        => SendMcp(client, accessToken, "resources/read", new { uri });

    private static Task<HttpResponseMessage> GetPrompt(HttpClient client, string accessToken, string name)
        => SendMcp(client, accessToken, "prompts/get", new { name });

    private static Task<HttpResponseMessage> SendMcp(
        HttpClient client, string accessToken, string method, object parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        request.Content = JsonContent.Create(new
        {
            jsonrpc = "2.0",
            id = 1,
            method,
            @params = parameters
        });
        return client.SendAsync(request);
    }
}
