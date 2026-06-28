using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kotlet.Api.IntegrationTests;

/// <summary>
/// Shared helpers for the home-aware auth flow: registered users start without a home, so most
/// tests need to create one (or join an existing one) before house-scoped endpoints work.
/// </summary>
internal static class TestAuth
{
    internal sealed record AuthedClient(HttpClient Client, string Email, Guid UserId, string AccessToken);

    internal static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    internal static async Task<AuthedClient> RegisterAsync(TestWebApplicationFactory factory, string prefix)
    {
        var client = factory.CreateClient();
        var email = $"{prefix}-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password = "Password1!", confirmPassword = "Password1!"
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;
        var userId = body.GetProperty("user").GetProperty("id").GetGuid();
        SetBearer(client, token);
        return new AuthedClient(client, email, userId, token);
    }

    /// <summary>Creates a home for an already-registered client and switches its token to that home.</summary>
    internal static async Task<Guid> CreateHomeAsync(HttpClient client, string name = "Test home")
    {
        var response = await client.PostAsJsonAsync("/api/houses", new { name });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        SetBearer(client, body.GetProperty("token").GetProperty("accessToken").GetString()!);
        return body.GetProperty("house").GetProperty("id").GetGuid();
    }

    /// <summary>Registers a fresh user and gives them their own home.</summary>
    internal static async Task<(HttpClient Client, Guid HouseId)> WithHomeAsync(TestWebApplicationFactory factory, string prefix)
    {
        var authed = await RegisterAsync(factory, prefix);
        var houseId = await CreateHomeAsync(authed.Client);
        return (authed.Client, houseId);
    }

    /// <summary>Invites <paramref name="member"/> to <paramref name="houseId"/> and accepts on their behalf.</summary>
    internal static async Task JoinHomeAsync(HttpClient ownerClient, Guid houseId, AuthedClient member)
    {
        (await ownerClient.PostAsJsonAsync($"/api/houses/{houseId}/members", new { email = member.Email }))
            .EnsureSuccessStatusCode();
        var invitations = await member.Client.GetFromJsonAsync<JsonElement>("/api/houses/invitations");
        var invitationId = invitations.EnumerateArray().First().GetProperty("id").GetGuid();
        var accept = await member.Client.PostAsync($"/api/houses/invitations/{invitationId}/accept", null);
        var body = await accept.Content.ReadFromJsonAsync<JsonElement>();
        SetBearer(member.Client, body.GetProperty("token").GetProperty("accessToken").GetString()!);
    }

    /// <summary>Two clients that share a single home: an owner and a joined member.</summary>
    internal static async Task<(HttpClient Owner, HttpClient Member, Guid HouseId)> HouseholdAsync(
        TestWebApplicationFactory factory, string prefix)
    {
        var (owner, houseId) = await WithHomeAsync(factory, $"{prefix}-owner");
        var member = await RegisterAsync(factory, $"{prefix}-member");
        await JoinHomeAsync(owner, houseId, member);
        return (owner, member.Client, houseId);
    }
}
