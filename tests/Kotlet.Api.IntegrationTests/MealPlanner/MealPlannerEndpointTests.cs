using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.MealPlanner;

public sealed class MealPlannerEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private const string Date = "2026-07-01";

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/meal-planner?date={Date}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/meal-planner/members")).StatusCode);
    }

    [Fact]
    public async Task NewMeal_HasNoParticipants_AndServingsDerivedFromHeadcount()
    {
        var client = await CreateAuthenticatedClient("mp-empty");
        var ingredientId = await CreateIngredient(client);
        var item = await AddIngredientMeal(client, ingredientId);

        Assert.Empty(item.GetProperty("participants").EnumerateArray());
        Assert.Equal(0, item.GetProperty("servings").GetInt32());
        Assert.False(item.GetProperty("servingsOverridden").GetBoolean());
    }

    [Fact]
    public async Task Overview_ReturnsEveryDayAndMarksPlannedSlots()
    {
        var client = await CreateAuthenticatedClient("mp-overview");
        var ingredientId = await CreateIngredient(client);
        await AddIngredientMeal(client, ingredientId);

        var overview = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/meal-planner/overview?from={Date}&days=3");

        Assert.NotNull(overview);
        Assert.Equal(3, overview.Length);
        Assert.Equal(Date, overview[0].GetProperty("date").GetString());
        Assert.Contains("dinner", overview[0].GetProperty("plannedSlots").EnumerateArray().Select(slot => slot.GetString()));
        Assert.Empty(overview[1].GetProperty("plannedSlots").EnumerateArray());
        Assert.Equal("2026-07-03", overview[2].GetProperty("date").GetString());
    }

    [Fact]
    public async Task AddingPeople_ScalesServingsByHeadcount()
    {
        var client = await CreateAuthenticatedClient("mp-people");
        var ingredientId = await CreateIngredient(client);
        var item = await AddIngredientMeal(client, ingredientId);
        var itemId = item.GetProperty("id").GetGuid();

        var members = await client.GetFromJsonAsync<JsonElement[]>("/api/meal-planner/members");
        var memberIds = members!.Select(m => m.GetProperty("userId").GetGuid()).ToArray();
        Assert.NotEmpty(memberIds);

        // Assign the first member: headcount drives servings.
        var oneResponse = await client.PutAsJsonAsync($"/api/meal-planner/items/{itemId}/participants",
            new { userIds = new[] { memberIds[0] } });
        Assert.Equal(HttpStatusCode.OK, oneResponse.StatusCode);
        var withOne = await oneResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(withOne.GetProperty("participants").EnumerateArray());
        Assert.Equal(1, withOne.GetProperty("servings").GetInt32());
        Assert.False(withOne.GetProperty("servingsOverridden").GetBoolean());

        // "Add whole house": every member assigned, servings == headcount.
        var allResponse = await client.PutAsJsonAsync($"/api/meal-planner/items/{itemId}/participants",
            new { userIds = memberIds });
        var withAll = await allResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(memberIds.Length, withAll.GetProperty("participants").EnumerateArray().Count());
        Assert.Equal(memberIds.Length, withAll.GetProperty("servings").GetInt32());
    }

    [Fact]
    public async Task ExplicitServings_OverrideHeadcount_AndCanBeReset()
    {
        var client = await CreateAuthenticatedClient("mp-servings");
        var ingredientId = await CreateIngredient(client);
        var item = await AddIngredientMeal(client, ingredientId);
        var itemId = item.GetProperty("id").GetGuid();

        var setResponse = await client.PutAsJsonAsync($"/api/meal-planner/items/{itemId}/servings", new { servings = 8 });
        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);
        var overridden = await setResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(8, overridden.GetProperty("servings").GetInt32());
        Assert.True(overridden.GetProperty("servingsOverridden").GetBoolean());

        var resetResponse = await client.PutAsJsonAsync($"/api/meal-planner/items/{itemId}/servings", new { servings = (int?)null });
        var reset = await resetResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, reset.GetProperty("servings").GetInt32());
        Assert.False(reset.GetProperty("servingsOverridden").GetBoolean());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync($"/api/meal-planner/items/{itemId}/servings", new { servings = -1 })).StatusCode);
    }

    [Fact]
    public async Task Guests_AddToHeadcount_AndAreValidated()
    {
        var client = await CreateAuthenticatedClient("mp-guests");
        var ingredientId = await CreateIngredient(client);
        var item = await AddIngredientMeal(client, ingredientId);
        var itemId = item.GetProperty("id").GetGuid();
        Assert.Equal(0, item.GetProperty("guests").GetInt32());

        // Two guests with no participants: servings derived from guest count.
        var setResponse = await client.PutAsJsonAsync($"/api/meal-planner/items/{itemId}/guests", new { guests = 2 });
        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);
        var withGuests = await setResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, withGuests.GetProperty("guests").GetInt32());
        Assert.Equal(2, withGuests.GetProperty("servings").GetInt32());
        Assert.False(withGuests.GetProperty("servingsOverridden").GetBoolean());

        // Adding a house member stacks on top of the guests.
        var members = await client.GetFromJsonAsync<JsonElement[]>("/api/meal-planner/members");
        var memberId = members![0].GetProperty("userId").GetGuid();
        var participantsResponse = await client.PutAsJsonAsync($"/api/meal-planner/items/{itemId}/participants",
            new { userIds = new[] { memberId } });
        var withBoth = await participantsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, withBoth.GetProperty("servings").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync($"/api/meal-planner/items/{itemId}/guests", new { guests = -1 })).StatusCode);
    }

    [Fact]
    public async Task SetParticipants_RejectsNonHouseMembers()
    {
        var client = await CreateAuthenticatedClient("mp-stranger");
        var ingredientId = await CreateIngredient(client);
        var item = await AddIngredientMeal(client, ingredientId);
        var itemId = item.GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync($"/api/meal-planner/items/{itemId}/participants",
            new { userIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HouseMembers_CanSeeAndManageTheSameMealPlan()
    {
        var (creator, houseMember, _) = await TestAuth.HouseholdAsync(factory, "mp-house");
        var ingredientId = await CreateIngredient(creator);
        var created = await AddIngredientMeal(creator, ingredientId);
        var itemId = created.GetProperty("id").GetGuid();

        var plan = await houseMember.GetFromJsonAsync<JsonElement>($"/api/meal-planner?date={Date}");
        var visibleItem = plan.GetProperty("meals").GetProperty("dinner").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == itemId);
        Assert.Equal(created.GetProperty("displayName").GetString(), visibleItem.GetProperty("displayName").GetString());

        var servingsResponse = await houseMember.PutAsJsonAsync(
            $"/api/meal-planner/items/{itemId}/servings", new { servings = 4 });
        Assert.Equal(HttpStatusCode.OK, servingsResponse.StatusCode);

        var deleteResponse = await houseMember.DeleteAsync($"/api/meal-planner/items/{itemId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    private async Task<JsonElement> AddIngredientMeal(HttpClient client, Guid ingredientId)
    {
        var response = await client.PostAsJsonAsync("/api/meal-planner/items",
            new { date = Date, slot = "dinner", ingredientId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Guid> CreateIngredient(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/ingredients", new
        {
            name = $"Meal ingredient {Guid.NewGuid():N}", measurementUnit = "g",
            caloriesPer100BaseUnits = 100m, pricePer100BaseUnits = 5m
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> CreateAuthenticatedClient(string prefix)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"{prefix}-{Guid.NewGuid():N}@example.com", password = "Password1!", confirmPassword = "Password1!"
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        await TestAuth.CreateHomeAsync(client);
        return client;
    }
}
