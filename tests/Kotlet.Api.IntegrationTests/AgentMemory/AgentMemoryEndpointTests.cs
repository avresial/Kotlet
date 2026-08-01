using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.AgentMemory;

public sealed class AgentMemoryEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/agent-memory");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Memory_CanBeCreatedVersionedUpdatedDeletedAndExported()
    {
        var (client, _) = await TestAuth.WithHomeAsync(factory, "memory-crud");
        var invalidFilter = await client.GetAsync("/api/agent-memory?source=Explicit");
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);

        var create = await client.PostAsJsonAsync("/api/agent-memory", new
        {
            content = "User prefers vegetarian dinners.",
            title = "Dinner preference",
            category = "food",
            source = "UserProvidedFact",
            reviewStatus = "Confirmed",
            clientId = "test"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var memory = created.GetProperty("memory");
        var id = memory.GetProperty("id").GetGuid();
        Assert.Equal(1, memory.GetProperty("version").GetInt64());

        var update = await client.PutAsJsonAsync($"/api/agent-memory/{id}", new
        {
            expectedVersion = 1,
            content = "User prefers vegetarian dinners with lentils.",
            title = "Dinner preference",
            category = "food",
            source = "UserCorrection",
            reviewStatus = "Confirmed"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(2, (await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("memory").GetProperty("version").GetInt64());

        var stale = await client.PutAsJsonAsync($"/api/agent-memory/{id}", new
        {
            expectedVersion = 1,
            content = "Stale edit",
            source = "ExplicitRequest"
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var delete = await client.DeleteAsync($"/api/agent-memory/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        var deleteWithVersion = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/agent-memory/{id}?expectedVersion=2&clientId=test");
        var deleted = await client.SendAsync(deleteWithVersion);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        var changes = await client.GetFromJsonAsync<JsonElement>("/api/agent-memory/changes?since=0");
        Assert.Contains(changes.GetProperty("changes").EnumerateArray(), item => item.GetProperty("deleted").GetBoolean());
        var exported = await client.GetFromJsonAsync<JsonElement>("/api/agent-memory/export");
        Assert.DoesNotContain("vegetarian", exported.GetProperty("markdown").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Memory_IsNotVisibleToAnotherAccount()
    {
        var (owner, _) = await TestAuth.WithHomeAsync(factory, "memory-owner");
        var (other, _) = await TestAuth.WithHomeAsync(factory, "memory-other");
        await (await owner.PostAsJsonAsync("/api/agent-memory", new { content = "Private preference", source = "ExplicitRequest" })).Content.ReadAsStringAsync();

        var list = await other.GetFromJsonAsync<JsonElement>("/api/agent-memory");

        Assert.Empty(list.GetProperty("memories").EnumerateArray());
    }
}
