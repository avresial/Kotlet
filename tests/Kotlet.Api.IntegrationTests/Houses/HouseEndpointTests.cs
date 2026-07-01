using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Houses;

public sealed class HouseEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/houses")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task DashboardStats_ReturnHouseScopedCounts()
    {
        var (client, _) = await TestAuth.WithHomeAsync(factory, "dashboard-stats");
        var ingredientResponse = await client.PostAsJsonAsync("/api/ingredients", new
        {
            name = $"Stats ingredient {Guid.NewGuid():N}", measurementUnit = "g",
            caloriesPer100BaseUnits = 1m, pricePer100BaseUnits = 1m
        });
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ingredientId = ingredient.GetProperty("id").GetGuid();
        await client.PostAsJsonAsync("/api/pantry", new { ingredientId, quantity = 1m });
        await client.PostAsJsonAsync("/api/recipes", new
        {
            title = $"Stats recipe {Guid.NewGuid():N}", descriptionMarkdown = (string?)null,
            ingredients = Array.Empty<object>()
        });

        var stats = await client.GetFromJsonAsync<JsonElement>("/api/dashboard/stats");

        Assert.Equal(1, stats.GetProperty("recipeCount").GetInt32());
        Assert.Equal(1, stats.GetProperty("pantryItemCount").GetInt32());
    }

    [Fact]
    public async Task CreateHouse_ReturnsSummaryWithActivationTokenAndIsListed()
    {
        var authed = await TestAuth.RegisterAsync(factory, "house-create");

        var response = await authed.Client.PostAsJsonAsync("/api/houses", new { name = "My Home" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var house = body.GetProperty("house");

        Assert.Equal("My Home", house.GetProperty("name").GetString());
        Assert.Equal(1, house.GetProperty("memberCount").GetInt32());
        Assert.True(house.GetProperty("isDefault").GetBoolean());
        Assert.True(house.GetProperty("isActive").GetBoolean());
        // First home activates the session, so a fresh token is issued.
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("token").ValueKind);

        var houseId = house.GetProperty("id").GetGuid();
        var listed = await authed.Client.GetFromJsonAsync<JsonElement[]>("/api/houses");
        Assert.Contains(listed!, h => h.GetProperty("id").GetGuid() == houseId);
    }

    [Fact]
    public async Task CreateSecondHouse_DoesNotReissueToken()
    {
        var (client, _) = await TestAuth.WithHomeAsync(factory, "house-second");

        var response = await client.PostAsJsonAsync("/api/houses", new { name = "Holiday Home" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The caller already had an active home, so no re-activation token is returned.
        Assert.Equal(JsonValueKind.Null, body.GetProperty("token").ValueKind);
        Assert.False(body.GetProperty("house").GetProperty("isActive").GetBoolean());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateHouse_WithBlankName_ReturnsValidationProblem(string name)
    {
        var authed = await TestAuth.RegisterAsync(factory, "house-blank");

        var response = await authed.Client.PostAsJsonAsync("/api/houses", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHouse_ReturnsDetailWithMembersForMember()
    {
        var (client, houseId) = await TestAuth.WithHomeAsync(factory, "house-detail");

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/houses/{houseId}");

        Assert.Equal(houseId, detail.GetProperty("id").GetGuid());
        var members = detail.GetProperty("members").EnumerateArray().ToList();
        var self = Assert.Single(members);
        Assert.True(self.GetProperty("isCurrentUser").GetBoolean());
        Assert.Empty(detail.GetProperty("pendingInvitations").EnumerateArray());
    }

    [Fact]
    public async Task GetHouse_ForNonMember_ReturnsNotFound()
    {
        var (_, houseId) = await TestAuth.WithHomeAsync(factory, "house-owner");
        var (outsider, _) = await TestAuth.WithHomeAsync(factory, "house-outsider");

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/houses/{houseId}")).StatusCode);
    }

    [Fact]
    public async Task RenameHouse_UpdatesName()
    {
        var (client, houseId) = await TestAuth.WithHomeAsync(factory, "house-rename");

        var response = await client.PutAsJsonAsync($"/api/houses/{houseId}", new { name = "Renamed Home" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/houses/{houseId}");
        Assert.Equal("Renamed Home", detail.GetProperty("name").GetString());
    }

    [Fact]
    public async Task InviteAndAccept_AddsMemberToHouse()
    {
        var (owner, houseId) = await TestAuth.WithHomeAsync(factory, "house-invite-owner");
        var invitee = await TestAuth.RegisterAsync(factory, "house-invite-member");

        var invite = await owner.PostAsJsonAsync($"/api/houses/{houseId}/members", new { email = invitee.Email });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);

        var incoming = await invitee.Client.GetFromJsonAsync<JsonElement[]>("/api/houses/invitations");
        var invitation = Assert.Single(incoming!);
        Assert.Equal(houseId, invitation.GetProperty("houseId").GetGuid());

        var accept = await invitee.Client.PostAsync($"/api/houses/invitations/{invitation.GetProperty("id").GetGuid()}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/houses/{houseId}");
        Assert.Equal(2, detail.GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task InviteMember_WithUnknownEmail_ReturnsNotFound()
    {
        var (owner, houseId) = await TestAuth.WithHomeAsync(factory, "house-invite-unknown");

        var response = await owner.PostAsJsonAsync($"/api/houses/{houseId}/members",
            new { email = $"nobody-{Guid.NewGuid():N}@example.com" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InviteMember_WhoIsAlreadyAMember_ReturnsConflict()
    {
        var (owner, _, houseId) = await TestAuth.HouseholdAsync(factory, "house-dup-invite");
        // The helper already joined the second user; re-inviting that same member must conflict.
        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/houses/{houseId}");
        var memberEmail = detail.GetProperty("members").EnumerateArray()
            .First(m => !m.GetProperty("isCurrentUser").GetBoolean()).GetProperty("email").GetString();

        var response = await owner.PostAsJsonAsync($"/api/houses/{houseId}/members", new { email = memberEmail });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeclineInvitation_RemovesItWithoutJoining()
    {
        var (owner, houseId) = await TestAuth.WithHomeAsync(factory, "house-decline-owner");
        var invitee = await TestAuth.RegisterAsync(factory, "house-decline-member");
        await owner.PostAsJsonAsync($"/api/houses/{houseId}/members", new { email = invitee.Email });
        var incoming = await invitee.Client.GetFromJsonAsync<JsonElement[]>("/api/houses/invitations");
        var invitationId = incoming!.Single().GetProperty("id").GetGuid();

        var decline = await invitee.Client.PostAsync($"/api/houses/invitations/{invitationId}/decline", null);
        Assert.Equal(HttpStatusCode.NoContent, decline.StatusCode);

        var remaining = await invitee.Client.GetFromJsonAsync<JsonElement[]>("/api/houses/invitations");
        Assert.Empty(remaining!);
    }

    [Fact]
    public async Task SwitchHouse_ReturnsAccessToken()
    {
        var (client, _) = await TestAuth.WithHomeAsync(factory, "house-switch");
        var second = await client.PostAsJsonAsync("/api/houses", new { name = "Second" });
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("house").GetProperty("id").GetGuid();

        var response = await client.PostAsync($"/api/houses/{secondId}/switch", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(token.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task RemoveMember_DropsThemFromTheHouse()
    {
        var (owner, _, houseId) = await TestAuth.HouseholdAsync(factory, "house-remove");
        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/houses/{houseId}");
        var memberId = detail.GetProperty("members").EnumerateArray()
            .First(m => !m.GetProperty("isCurrentUser").GetBoolean()).GetProperty("id").GetGuid();

        var response = await owner.DeleteAsync($"/api/houses/{houseId}/members/{memberId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await owner.GetFromJsonAsync<JsonElement>($"/api/houses/{houseId}");
        Assert.Equal(1, after.GetProperty("members").GetArrayLength());
    }
}
