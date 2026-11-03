using System.Net;
using System.Net.Http.Json;
using Kotlet.Application.Products;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Products;

public sealed class FoodDataCentralEndpointTests(FoodDataCentralWebApplicationFactory factory)
    : IClassFixture<FoodDataCentralWebApplicationFactory>
{
    private const string Code = "00048707820026";

    [Fact]
    public async Task PartialOffNutrition_IsSupplementedConfirmedAndReusedWithoutExternalRequests()
    {
        var client = await Client("mixed");
        var found = (await client.GetFromJsonAsync<ProductLookupResult>($"/api/products/lookup/{Code}"))!;
        Assert.Equal("open_food_facts", found.Status);
        Assert.Equal("OpenFoodFacts", found.Product!.Source);
        Assert.Equal(0, found.Product.ProteinPer100); // Known zero must never be overwritten by USDA's 10 g.
        Assert.Equal(267, found.Product.CaloriesPer100);
        Assert.Equal(2022017, found.Product.FoodDataCentralId);
        Assert.Empty((await client.GetFromJsonAsync<ProductDto[]>("/api/products"))!);
        var saved = await Save(client, found.Product);
        Assert.NotNull(saved.FoodDataCentralRetrievedAtUtc);
        Assert.Contains("openfoodfacts.org", saved.SourceUrl);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/product-stock", new { productId = saved.Id, quantity = 2 })).StatusCode);
        var before = (factory.Off.Calls, factory.Usda.Calls);
        var repeated = (await client.GetFromJsonAsync<ProductLookupResult>($"/api/products/lookup/{Code}"))!;
        Assert.Equal("local", repeated.Status);
        Assert.Equal(saved, repeated.Product);
        Assert.Equal(before, (factory.Off.Calls, factory.Usda.Calls));
        Assert.Single((await client.GetFromJsonAsync<ProductDto[]>("/api/products"))!);
    }

    [Fact]
    public async Task CompleteOffNutrition_DoesNotCallFallback()
    {
        var client = await Client("complete");
        var before = factory.Usda.Calls;
        var result = (await client.GetFromJsonAsync<ProductLookupResult>("/api/products/lookup/03017620422003"))!;
        Assert.Equal("open_food_facts", result.Status);
        Assert.Null(result.Product!.FoodDataCentralId);
        Assert.Equal(before, factory.Usda.Calls);
    }

    [Fact]
    public async Task UnknownOffProduct_UsesUsdaAndPreservesProvenanceOnCorrection()
    {
        var client = await Client("usda-only");
        var found = (await client.GetFromJsonAsync<ProductLookupResult>("/api/products/lookup/00000096385074"))!;
        Assert.Equal("food_data_central", found.Status);
        Assert.Equal("FoodDataCentral", found.Product!.Source);
        Assert.Equal("pcs", found.Product.Unit);
        Assert.Equal(1, found.Product.PackageQuantity);
        var saved = await Save(client, found.Product);
        Assert.Equal("https://fdc.nal.usda.gov/food-details/2022017/nutrients", saved.SourceUrl);
        var correctedResponse = await client.PutAsJsonAsync($"/api/products/{saved.Barcode}", new SaveProductCommand("Corrected", CaloriesPer100: 200, NutritionBasis: "g"));
        var corrected = await correctedResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal("FoodDataCentral", corrected!.Source);
        Assert.Equal(saved.FoodDataCentralId, corrected.FoodDataCentralId);
    }

    [Fact]
    public async Task IncompatibleMassAndVolumeBases_AreNotMerged()
    {
        var client = await Client("volume");
        var result = (await client.GetFromJsonAsync<ProductLookupResult>("/api/products/lookup/00036000291452"))!;
        Assert.Equal("ml", result.Product!.NutritionBasis);
        Assert.Equal(0, result.Product.ProteinPer100);
        Assert.Null(result.Product.CaloriesPer100);
        Assert.Null(result.Product.FoodDataCentralId);
    }

    [Fact]
    public async Task ExistingIncompleteCatalog_HasExplicitReviewBeforeEnrichmentAndKeepsPackageAndCorrections()
    {
        var client = await Client("refresh");
        var response = await client.PutAsJsonAsync($"/api/products/{Code}", new SaveProductCommand("My corrected name", PackageQuantity: 500, Unit: "g", CaloriesPer100: 0, NutritionBasis: "g"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var original = (await response.Content.ReadFromJsonAsync<ProductDto>())!;
        var before = (factory.Off.Calls, factory.Usda.Calls);
        await client.GetFromJsonAsync<ProductLookupResult>($"/api/products/lookup/{Code}");
        Assert.Equal(before, (factory.Off.Calls, factory.Usda.Calls));
        var draft = (await client.GetFromJsonAsync<ProductLookupResult>($"/api/products/lookup/{Code}?refreshNutrition=true"))!.Product!;
        Assert.Equal(original.Id, draft.Id);
        Assert.Equal("My corrected name", draft.Name);
        Assert.Equal(500, draft.PackageQuantity);
        Assert.Equal("g", draft.Unit);
        Assert.Equal(0, draft.CaloriesPer100);
        Assert.Equal(20, draft.FatPer100);
        var unchanged = Assert.Single((await client.GetFromJsonAsync<ProductDto[]>("/api/products"))!);
        Assert.Null(unchanged.FatPer100);
        var saved = await Save(client, draft);
        Assert.Equal(original.Id, saved.Id);
        Assert.Equal(20, saved.FatPer100);
        Assert.Equal(2022017, saved.FoodDataCentralId);
    }

    [Fact]
    public async Task MissingFallback_LeavesManualEntryAndSaveAvailable()
    {
        var client = await Client("missing");
        var lookup = (await client.GetFromJsonAsync<ProductLookupResult>("/api/products/lookup/01234567890128"))!;
        Assert.Equal("not_found", lookup.Status);
        Assert.Null(lookup.Product);
        var saved = await client.PutAsJsonAsync("/api/products/01234567890128", new SaveProductCommand("Manual product"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidSourceRecord_IsRejected(int id)
    {
        var client = await Client("invalid");
        var response = await client.PutAsJsonAsync($"/api/products/{Code}", new SaveProductCommand("Food", Source: "FoodDataCentral", FoodDataCentralId: id));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<ProductDto> Save(HttpClient client, ProductDto product)
    {
        var request = new SaveProductCommand(product.Name, product.Brand, product.PackageDescription, product.PackageQuantity,
            product.Unit, product.IngredientId, product.CaloriesPer100, product.ProteinPer100, product.CarbsPer100,
            product.FatPer100, product.NutritionBasis, product.Source, product.FoodDataCentralId);
        var response = await client.PutAsJsonAsync($"/api/products/{product.Barcode}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private async Task<HttpClient> Client(string prefix)
    {
        var (client, _) = await TestAuth.WithHomeAsync(factory, $"nutrition-{prefix}");
        return client;
    }
}

public sealed class FoodDataCentralWebApplicationFactory : TestWebApplicationFactory
{
    public NutritionOffClient Off { get; } = new();
    public NutritionUsdaClient Usda { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOpenFoodFactsClient>();
            services.RemoveAll<IFoodDataCentralClient>();
            services.AddSingleton<IOpenFoodFactsClient>(Off);
            services.AddSingleton<IFoodDataCentralClient>(Usda);
        });
    }
}

public sealed class NutritionOffClient : IOpenFoodFactsClient
{
    public int Calls { get; private set; }
    public Task<ProductLookupResult> LookupAsync(string barcode, CancellationToken cancellationToken)
    {
        Calls++;
        var product = barcode switch
        {
            "00048707820026" => Product(barcode, null, 0, null, null, "g"),
            "03017620422003" => Product(barcode, 250, 12, 30, 10, "g"),
            "00036000291452" => Product(barcode, null, 0, null, null, "ml"),
            _ => null
        };
        return Task.FromResult(product is null ? new ProductLookupResult("not_found") : new("open_food_facts", product));
    }
    private static ProductDto Product(string code, decimal? calories, decimal? protein, decimal? carbs, decimal? fat, string basis) =>
        new(Guid.Empty, code, "OFF name", "OFF brand", "500 g", 500, "g", null, calories, protein, carbs, fat, basis,
            "OpenFoodFacts", $"https://world.openfoodfacts.org/product/{code}", DateTimeOffset.UtcNow);
}

public sealed class NutritionUsdaClient : IFoodDataCentralClient
{
    public int Calls { get; private set; }
    public Task<ProductDto?> LookupAsync(string barcode, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<ProductDto?>(barcode == "01234567890128" ? null :
            new(Guid.Empty, barcode, "USDA name", "USDA brand", null, 1, "pcs", null, 267, 10, 13.333m, 20, "g",
                "FoodDataCentral", "https://fdc.nal.usda.gov/food-details/2022017/nutrients", DateTimeOffset.UtcNow, 2022017));
    }
}
