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
                     "get_recipes", "get_recipe", "get_ingredients",
                     "get_prepared_meals", "get_prepared_meal", "get_shopping_list", "get_pantry",
                     "get_meal_plan_overview", "get_meal_plan",
                     "get_meal_plan_members", "add_recipe", "create_ingredient",
                     "meal_plan_get_range", "meal_plan_replace", "meal_plan_move", "meal_plan_swap",
                     "meal_plan_clear_slot", "meal_plan_recommend_replacement", "meal_plan_apply_replacement",
                     "check_recipe_exists",
                     "add_prepared_meal", "update_prepared_meal", "remove_prepared_meal",
                     "add_pantry_item", "update_pantry_item", "remove_pantry_item",
                     "add_meal_to_plan", "add_meal_participants", "set_meal_participants",
                     "set_meal_participant_portion", "set_meal_guests", "set_meal_servings",
                     "move_meal_in_plan", "copy_meal_plan_day", "copy_meal_plan_week",
                     "remove_meal_from_plan"
                 })
            Assert.Contains($"\"{tool}\"", body);
    }

    [Fact]
    public async Task PreparedMeals_AreBrowsableAndEditableThroughTools()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var name = $"Frozen curry {Guid.NewGuid():N}";

        var added = await CallTool(client, accessToken, "add_prepared_meal", new
        {
            request = new
            {
                name,
                description = "A quick freezer meal",
                brand = "Kotlet Foods",
                store = "Local market",
                category = "Dinner",
                packageQuantity = 400,
                packageUnit = "g",
                servings = 2,
                caloriesPerServing = 350,
                price = 12.50m,
                preparationInstructions = "Heat for 8 minutes.",
                shoppingIngredientId = (Guid?)null,
                addons = Array.Empty<object>()
            }
        });
        var addedBody = await added.Content.ReadAsStringAsync();
        Assert.Contains("\"Success\"", addedBody);
        var preparedMealId = ExtractGuidAfter(addedBody, "\"id\":\"");

        var list = await CallTool(client, accessToken, "get_prepared_meals", new { });
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.Contains(name, listBody);
        Assert.DoesNotContain("Heat for 8 minutes.", listBody);

        var updatedName = $"Updated {name}";
        var updated = await CallTool(client, accessToken, "update_prepared_meal", new
        {
            preparedMealId,
            request = new
            {
                name = updatedName,
                description = "Ready even faster",
                brand = "Kotlet Foods",
                store = "Local market",
                category = "Dinner",
                packageQuantity = 400,
                packageUnit = "g",
                servings = 2,
                caloriesPerServing = 340,
                price = 11.50m,
                preparationInstructions = "Heat for 7 minutes.",
                shoppingIngredientId = (Guid?)null,
                addons = Array.Empty<object>()
            }
        });
        Assert.Contains(updatedName, await updated.Content.ReadAsStringAsync());

        var detail = await CallTool(client, accessToken, "get_prepared_meal", new { preparedMealId });
        Assert.Contains(updatedName, await detail.Content.ReadAsStringAsync());

        var resource = await SendMcp(
            client,
            accessToken,
            "resources/read",
            new { uri = $"kotlet://prepared-meals/{preparedMealId}" });
        Assert.Contains(updatedName, await resource.Content.ReadAsStringAsync());

        var removed = await CallTool(client, accessToken, "remove_prepared_meal", new { preparedMealId });
        Assert.Contains("true", (await removed.Content.ReadAsStringAsync()).ToLowerInvariant());

        var active = await CallTool(client, accessToken, "get_prepared_meals", new { });
        Assert.DoesNotContain(updatedName, await active.Content.ReadAsStringAsync());

        var archived = await CallTool(client, accessToken, "get_prepared_meals", new { includeArchived = true });
        var archivedBody = await archived.Content.ReadAsStringAsync();
        Assert.Contains(updatedName, archivedBody);
        Assert.Contains("\"isArchived\":true", archivedBody);
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
    public async Task GetIngredients_ReturnsClosestMatchesInInputOrder()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var appleName = $"Apple {suffix}";
        var pearName = $"Pear {suffix}";
        var apple = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new { name = appleName, measurementUnit = "g", caloriesPer100BaseUnits = 52 }
        });
        var appleId = ExtractGuidAfter(await apple.Content.ReadAsStringAsync(), "\"id\":\"");
        var pear = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new { name = pearName, measurementUnit = "g", caloriesPer100BaseUnits = 57 }
        });
        var pearId = ExtractGuidAfter(await pear.Content.ReadAsStringAsync(), "\"id\":\"");

        var response = await CallTool(client, accessToken, "get_ingredients", new
        {
            names = new[] { $"Aoole {suffix}", pearName }
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(body.IndexOf(appleId.ToString(), StringComparison.OrdinalIgnoreCase)
                    < body.IndexOf(pearId.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(appleName, body);
        Assert.Contains("\"distance\":2", body);
        Assert.Contains("\"similarity\":", body);
        Assert.Contains("\"matchedLanguage\":\"en\"", body);
        Assert.Contains("\"measurementUnit\":\"g\"", body);
        Assert.Contains("\"exactMatch\":false", body);
        Assert.Contains($"kotlet://ingredients/{appleId}", body);
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
            ingredientId,
            quantity = 1000,
            storageLocation = "Refrigerator",
            expirationDate = "2027-01-15"
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
            ingredientId,
            quantity = 10,
            storageLocation = "Garage"
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

    [Fact]
    public async Task MealPlanning_AddsRecipeMeal_AssignsMember_ThenRemovesIt()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        // A recipe the agent can plan needs an ingredient and a recipe first.
        var created = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new { name = $"Tomato {Guid.NewGuid():N}", measurementUnit = "g", caloriesPer100BaseUnits = 18 }
        });
        var ingredientId = ExtractGuidAfter(await created.Content.ReadAsStringAsync(), "\"id\":\"");
        var recipe = await CallTool(client, accessToken, "add_recipe", new
        {
            request = new
            {
                title = $"Tomato soup {Guid.NewGuid():N}",
                servings = 2,
                descriptionMarkdown = "1. Simmer tomatoes.",
                ingredients = new[] { new { ingredientId, quantity = 400, unit = "g" } }
            }
        });
        var recipeId = ExtractGuidAfter(await recipe.Content.ReadAsStringAsync(), "\"id\":\"");

        // Plan the recipe onto a specific day and slot.
        var added = await CallTool(client, accessToken, "add_meal_to_plan", new
        {
            request = new { date = "2026-07-10", slot = "dinner", recipeId }
        });
        var addedBody = await added.Content.ReadAsStringAsync();
        Assert.Contains("\"Success\"", addedBody);
        var mealId = ExtractGuidAfter(addedBody, "\"id\":\"");

        // The registering user is a member of the house and can be assigned to the meal.
        var members = await CallTool(client, accessToken, "get_meal_plan_members", new { });
        var memberId = ExtractGuidAfter(await members.Content.ReadAsStringAsync(), "\"userId\":\"");
        var assigned = await CallTool(client, accessToken, "add_meal_participants", new
        {
            mealId,
            userIds = new[] { memberId }
        });
        var assignedBody = await assigned.Content.ReadAsStringAsync();
        Assert.Contains("\"Success\"", assignedBody);
        Assert.Contains(memberId.ToString(), assignedBody);

        var portion = await CallTool(client, accessToken, "set_meal_participant_portion", new
        {
            mealId,
            userId = memberId,
            portionPercent = 150
        });
        var portionBody = await portion.Content.ReadAsStringAsync();
        Assert.Contains("\"portionPercent\":150", portionBody);
        Assert.Contains("\"servings\":1.5", portionBody);

        // The meal now shows up in the day's plan.
        var plan = await CallTool(client, accessToken, "get_meal_plan", new { from = "2026-07-10", days = 1 });
        Assert.Contains(mealId.ToString(), await plan.Content.ReadAsStringAsync());

        // Removing the meal reports success and clears it from the plan.
        var removed = await CallTool(client, accessToken, "remove_meal_from_plan", new { mealId });
        Assert.Contains("true", (await removed.Content.ReadAsStringAsync()).ToLowerInvariant());
        var afterRemoval = await CallTool(client, accessToken, "remove_meal_from_plan", new { mealId });
        Assert.Contains("false", (await afterRemoval.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task PlanningTools_ReturnCompactAgentResultsAndKeepFullDataInUiMetadata()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var ingredientName = $"Compact tomato {Guid.NewGuid():N}";

        var created = ToolResult(await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new { name = ingredientName, measurementUnit = "g", caloriesPer100BaseUnits = 18 }
        }));
        AssertShortText(created, "Created ingredient");
        var createdCompact = created.GetProperty("structuredContent");
        var ingredientId = createdCompact.GetProperty("ingredientId").GetGuid();
        Assert.False(createdCompact.TryGetProperty("ingredient", out _));
        Assert.Equal(18, created.GetProperty("_meta").GetProperty("kotlet/uiData")
            .GetProperty("ingredient").GetProperty("caloriesPer100BaseUnits").GetDecimal());

        var matches = ToolResult(await CallTool(client, accessToken, "get_ingredients", new
        {
            names = new[] { ingredientName }
        }));
        AssertShortText(matches, "Matched 1 ingredient");
        var compactMatch = matches.GetProperty("structuredContent").GetProperty("matches")[0];
        Assert.Equal(ingredientId, compactMatch.GetProperty("ingredientId").GetGuid());
        Assert.False(compactMatch.TryGetProperty("distance", out _));
        Assert.True(matches.GetProperty("_meta").GetProperty("kotlet/uiData")[0]
            .TryGetProperty("distance", out _));

        var members = ToolResult(await CallTool(client, accessToken, "get_meal_plan_members", new { }));
        AssertShortText(members, "Found 1 household member");
        var memberId = members.GetProperty("structuredContent").GetProperty("members")[0]
            .GetProperty("userId").GetGuid();
        Assert.Equal(JsonValueKind.Array,
            members.GetProperty("_meta").GetProperty("kotlet/uiData").ValueKind);

        var request = new
        {
            weekStart = "2027-04-05",
            meals = new[]
            {
                new { date = "2027-04-05", slot = "breakfast", ingredientId }
            }
        };
        var added = ToolResult(await CallTool(client, accessToken, "add_weekly_meal_plan", new { request }));
        AssertShortText(added, "Added 1 meal");
        var addedCompact = added.GetProperty("structuredContent");
        var mealId = addedCompact.GetProperty("mealIds")[0].GetGuid();
        Assert.Equal(1, addedCompact.GetProperty("addedCount").GetInt32());
        Assert.False(addedCompact.TryGetProperty("plan", out _));
        Assert.True(added.GetProperty("_meta").GetProperty("kotlet/uiData")
            .GetProperty("plan").TryGetProperty("added", out _));

        var assigned = ToolResult(await CallTool(client, accessToken, "set_meal_participants", new
        {
            mealId,
            userIds = new[] { memberId }
        }));
        AssertShortText(assigned, "Updated meal participants");
        var assignedCompact = assigned.GetProperty("structuredContent");
        Assert.Equal(1, assignedCompact.GetProperty("participantCount").GetInt32());
        Assert.False(assignedCompact.TryGetProperty("item", out _));
        Assert.Single(assigned.GetProperty("_meta").GetProperty("kotlet/uiData")
            .GetProperty("item").GetProperty("participants").EnumerateArray());

        var plan = ToolResult(await CallTool(
            client, accessToken, "get_meal_plan", new { from = "2027-04-05", days = 1 }));
        AssertShortText(plan, "Retrieved 1 meal-plan day");
        var compactMeal = plan.GetProperty("structuredContent").GetProperty("days")[0]
            .GetProperty("meals")[0];
        Assert.Equal(mealId, compactMeal.GetProperty("id").GetGuid());
        Assert.False(compactMeal.TryGetProperty("sortOrder", out _));
        Assert.True(plan.GetProperty("_meta").GetProperty("kotlet/uiData")[0]
            .GetProperty("meals").GetProperty("breakfast")[0].TryGetProperty("sortOrder", out _));
    }

    [Fact]
    public async Task ToolsList_DeclaresCompactOutputSchemas()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var response = await SendMcp(client, accessToken, "tools/list", new { });
        var result = ToolResult(response, "result");
        var compactNames = new HashSet<string>
        {
            "get_ingredients", "create_ingredient", "get_meal_plan_members",
            "get_meal_plan", "add_weekly_meal_plan", "set_meal_participants"
        };
        var tools = result.GetProperty("tools").EnumerateArray()
            .Where(tool => compactNames.Contains(tool.GetProperty("name").GetString()!))
            .ToDictionary(
            tool => tool.GetProperty("name").GetString()!,
            tool => tool.GetProperty("outputSchema"));

        Assert.True(tools["get_ingredients"].GetProperty("properties").TryGetProperty("matches", out _));
        Assert.True(tools["create_ingredient"].GetProperty("properties").TryGetProperty("ingredientId", out _));
        Assert.True(tools["get_meal_plan_members"].GetProperty("properties").TryGetProperty("members", out _));
        Assert.True(tools["get_meal_plan"].GetProperty("properties").TryGetProperty("days", out _));
        Assert.True(tools["add_weekly_meal_plan"].GetProperty("properties").TryGetProperty("mealIds", out _));
        Assert.True(tools["set_meal_participants"].GetProperty("properties").TryGetProperty("participantCount", out _));
    }

    private Task<(HttpClient Client, string AccessToken)> AuthorizeMcpClientAsync()
        => McpTestHelpers.AuthorizeMcpClientAsync(factory, "mcp-browse");

    private static Guid ExtractGuidAfter(string body, string marker)
        => McpTestHelpers.ExtractGuidAfter(body, marker);

    private static Task<HttpResponseMessage> CallTool(
        HttpClient client, string accessToken, string name, object arguments,
        string protocolVersion = McpTestHelpers.DefaultProtocolVersion)
        => McpTestHelpers.CallTool(client, accessToken, name, arguments, protocolVersion);

    private static JsonElement ToolResult(HttpResponseMessage response, string property = "result")
        => McpTestHelpers.ToolResult(response, property);

    private static void AssertShortText(JsonElement result, string expected)
        => McpTestHelpers.AssertShortText(result, expected);

    private static Task<HttpResponseMessage> SendMcp(
        HttpClient client, string accessToken, string method, object parameters,
        string protocolVersion = McpTestHelpers.DefaultProtocolVersion)
        => McpTestHelpers.SendMcp(client, accessToken, method, parameters, protocolVersion);
}
