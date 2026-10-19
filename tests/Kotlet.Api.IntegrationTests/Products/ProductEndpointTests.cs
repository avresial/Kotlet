using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kotlet.Application.Products;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Products;

public sealed class ProductEndpointTests(ProductWebApplicationFactory factory) : IClassFixture<ProductWebApplicationFactory>
{
    private const string Code = "3017620422003";

    [Fact]
    public async Task ProductEndpoints_RequireAuthentication()
    {
        var client = factory.CreateClient();
        foreach (var path in new[] { "/api/products", $"/api/products/lookup/{Code}", "/api/product-stock", "/api/products/export" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"/api/products/{Code}", Product())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/product-stock", new { productId = Guid.NewGuid(), quantity = 1 })).StatusCode);
    }

    [Fact]
    public async Task Scan_Confirm_RepeatAndCorrectBatches_PreservesExistingPantry()
    {
        var client = await Client("products");
        var beforeLookup = factory.Off.Calls;
        var lookup = await client.GetFromJsonAsync<ProductLookupResult>($"/api/products/lookup/{Code}");
        Assert.Equal("open_food_facts", lookup!.Status);
        Assert.Empty((await client.GetFromJsonAsync<ProductDto[]>("/api/products"))!);
        var saved = await Save(client);
        var secondSave = await client.PutAsJsonAsync("/api/products/03017620422003", Product());
        Assert.Equal(saved.Id, (await secondSave.Content.ReadFromJsonAsync<ProductDto>())!.Id);
        Assert.Single((await client.GetFromJsonAsync<ProductDto[]>("/api/products"))!);
        var local = await client.GetFromJsonAsync<ProductLookupResult>("/api/products/lookup/03017620422003");
        Assert.Equal("local", local!.Status);
        Assert.Equal(beforeLookup + 1, factory.Off.Calls);

        var first = await Add(client, saved.Id, 1000, "2027-01-01", 1);
        var repeat = await Add(client, saved.Id, 500, "2027-01-01", 1);
        Assert.Equal(first.Id, repeat.Id);
        Assert.Equal(1500, repeat.Quantity);
        var later = await Add(client, saved.Id, 500, "2027-02-01", 1);
        Assert.NotEqual(first.Id, later.Id);
        var cabinet = await Add(client, saved.Id, 500, "2027-01-01", 3);
        Assert.NotEqual(first.Id, cabinet.Id);
        Assert.Equal(3, (await client.GetFromJsonAsync<ProductStockDto[]>("/api/product-stock"))!.Length);
        var update = await client.PutAsJsonAsync($"/api/product-stock/{later.Id}", new { quantity = 250, expirationDate = "2027-03-01", storageLocation = 2 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<ProductStockDto>();
        Assert.Equal(250, updated!.Quantity);
        Assert.Equal(new DateOnly(2027, 3, 1), updated.ExpirationDate);
        Assert.Equal(2, (int)updated.StorageLocation!);
        var collision = await client.PutAsJsonAsync($"/api/product-stock/{later.Id}", new { quantity = 250, expirationDate = "2027-01-01", storageLocation = 1 });
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/product-stock/{later.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/pantry"))!);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/shopping-list"))!);
    }

    [Fact]
    public async Task ManualProduct_UnknownAndOfflineFallback_RemainsHouseScoped()
    {
        var (owner, housemate, _) = await TestAuth.HouseholdAsync(factory, "product-house");
        var outsider = await Client("product-outsider");
        var missing = await owner.GetFromJsonAsync<ProductLookupResult>("/api/products/lookup/96385074");
        Assert.Equal("not_found", missing!.Status);
        var offline = await owner.GetFromJsonAsync<ProductLookupResult>("/api/products/lookup/036000291452");
        Assert.Equal("unavailable", offline!.Status);
        var response = await owner.PutAsJsonAsync("/api/products/96385074", new { name = "Manual product", packageQuantity = 1, unit = "pcs" });
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal("Manual", product!.Source);
        Assert.Null(product.CaloriesPer100);
        var first = await Add(owner, product.Id, 1, null, null);
        var same = await Add(housemate, product.Id, 1, null, null);
        Assert.Equal(first.Id, same.Id);
        Assert.Equal(2, same.Quantity);
        Assert.Single((await housemate.GetFromJsonAsync<ProductDto[]>("/api/products"))!);
        Assert.Empty((await outsider.GetFromJsonAsync<ProductDto[]>("/api/products"))!);
        Assert.Empty((await outsider.GetFromJsonAsync<ProductStockDto[]>("/api/product-stock"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync("/api/product-stock", new { productId = product.Id, quantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PutAsJsonAsync($"/api/product-stock/{first.Id}", new { quantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.DeleteAsync($"/api/product-stock/{first.Id}")).StatusCode);
    }

    [Fact]
    public async Task Validation_RejectsInvalidCodesQuantitiesUnitsAndNutrition_AndKeepsAttribution()
    {
        var client = await Client("product-validation");
        foreach (var code in new[] { "3017620422004", "00000000", "abcdefghi" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/products/lookup/{code}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/products/{code}", Product())).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/products/{Code}", new { name = "", unit = "kg", packageQuantity = -1, proteinPer100 = 101 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/products/{Code}", new { name = "Food", caloriesPer100 = 100 })).StatusCode);
        var saved = await Save(client);
        await Add(client, saved.Id, 1000, null, null);
        foreach (var quantity in new[] { -1m, 0m, 100000000m, 0.0001m })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/product-stock", new { productId = saved.Id, quantity })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/product-stock", new { productId = saved.Id, quantity = 1, storageLocation = 99 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/products/{Code}", new { name = "Food", packageQuantity = 1, unit = "pcs" })).StatusCode);
        var correctedResponse = await client.PutAsJsonAsync($"/api/products/{Code}", new { name = "Corrected label", packageQuantity = 500, unit = "g", source = "Manual" });
        var corrected = await correctedResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal("OpenFoodFacts", corrected!.Source);
        Assert.NotNull(corrected.SourceUrl);
        var export = await client.GetFromJsonAsync<JsonElement>("/api/products/export");
        Assert.Contains("ODbL", export.GetProperty("attribution").GetString());
        Assert.False(export.TryGetProperty("stock", out _));
    }

    [Fact]
    public async Task ProductNutrition_IsOptionalAndUsesConfirmedBasisAndExistingIngredient()
    {
        var client = await Client("product-nutrition");
        var ingredientResponse = await client.PostAsJsonAsync("/api/ingredients", new
        {
            name = $"Oats {Guid.NewGuid():N}",
            measurementUnit = "g",
            caloriesPer100BaseUnits = 370,
            pricePer100BaseUnits = 2
        });
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ingredientId = ingredient.GetProperty("id").GetGuid();
        var response = await client.PutAsJsonAsync($"/api/products/{Code}", new
        {
            name = "Oat drink",
            packageQuantity = 1000,
            unit = "ml",
            ingredientId,
            nutritionBasis = "ml",
            caloriesPer100 = 40.12345m,
            fatPer100 = 0m
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal(ingredientId, saved!.IngredientId);
        Assert.Equal(40.123m, saved.CaloriesPer100);
        Assert.Equal(0m, saved.FatPer100);
        Assert.Null(saved.ProteinPer100);
        Assert.Equal("ml", saved.NutritionBasis);
        var read = Assert.Single((await client.GetFromJsonAsync<ProductDto[]>("/api/products"))!);
        Assert.Equal(saved, read);
        var unknownIngredient = await client.PutAsJsonAsync($"/api/products/{Code}", new
        {
            name = "Food",
            ingredientId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknownIngredient.StatusCode);
    }

    private static object Product() => new { name = "Food", brand = "Brand", packageQuantity = 500, unit = "g", source = "OpenFoodFacts" };
    private static async Task<ProductDto> Save(HttpClient client)
    {
        var response = await client.PutAsJsonAsync($"/api/products/{Code}", Product());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }
    private static async Task<ProductStockDto> Add(HttpClient client, Guid productId, decimal quantity, string? expirationDate, int? storageLocation)
    {
        var response = await client.PostAsJsonAsync("/api/product-stock", new { productId, quantity, expirationDate, storageLocation });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductStockDto>())!;
    }
    private async Task<HttpClient> Client(string prefix)
    {
        var (client, _) = await TestAuth.WithHomeAsync(factory, prefix);
        return client;
    }
}

public sealed class ProductWebApplicationFactory : TestWebApplicationFactory
{
    public FakeOffClient Off { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOpenFoodFactsClient>();
            services.AddSingleton<IOpenFoodFactsClient>(Off);
        });
    }
}

public sealed class FakeOffClient : IOpenFoodFactsClient
{
    public int Calls { get; private set; }
    public Task<ProductLookupResult> LookupAsync(string normalizedBarcode, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(normalizedBarcode switch
        {
            "03017620422003" => new ProductLookupResult("open_food_facts", new ProductDto(Guid.Empty, normalizedBarcode, "Food", "Brand", "500 g", 500, "g", null, null, null, null, null, null, "OpenFoodFacts", "https://world.openfoodfacts.org/product/3017620422003", DateTimeOffset.UtcNow)),
            "00000096385074" => new ProductLookupResult("not_found"),
            _ => new ProductLookupResult("unavailable")
        });
    }
}
