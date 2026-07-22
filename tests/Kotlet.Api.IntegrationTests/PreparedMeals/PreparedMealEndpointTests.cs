using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.PreparedMeals;

public sealed class PreparedMealEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Create_RequiresCaloriesPerServing()
    {
        var client = factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"prepared-meal-{Guid.NewGuid():N}@example.com",
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        var body = await registration.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        await TestAuth.CreateHomeAsync(client);

        var response = await client.PostAsJsonAsync("/api/prepared-meals", new
        {
            name = "Meal without calories",
            servings = 1,
            addons = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
