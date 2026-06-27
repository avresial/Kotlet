using System.Net;
using System.Net.Http.Json;
using Kotlet.Domain.Menu;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kotlet.Api.IntegrationTests.Menu;

public sealed class GetMenuEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task GetMenu_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/menu");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMenu_ReturnsSeededEntries()
    {
        var client = _factory.CreateClient();

        var entries = await client.GetFromJsonAsync<IReadOnlyCollection<MenuEntry>>("/api/menu");

        Assert.NotNull(entries);
        Assert.NotEmpty(entries);
    }
}
