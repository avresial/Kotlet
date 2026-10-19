using System.Net;
using System.Text;
using System.Text.Json;
using Kotlet.Infrastructure.Products;
using Xunit;

namespace Kotlet.Infrastructure.UnitTests.Products;

public sealed class OpenFoodFactsClientTests
{
    private const string Code = "03017620422003";
    private const string Payload = """
        {"status":"success","product":{"code":"3017620422003","product_name":"Food","brands":"Brand","quantity":"500 g","product_quantity":"500","product_quantity_unit":"g","nutriments":{"energy-kcal_100g":250,"proteins_100g":"12.5","fat_100g":-1,"carbohydrates_100g":999}}}
        """;

    [Fact]
    public async Task Lookup_CachesValidatedPrefillIncludingMissingNutrients()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(Payload, Encoding.UTF8, "application/json") });
        using var client = new OpenFoodFactsClient(new Factory(handler), TimeProvider.System);
        var first = await client.LookupAsync(Code, CancellationToken.None);
        var second = await client.LookupAsync(Code, CancellationToken.None);
        Assert.Equal("open_food_facts", first.Status);
        Assert.Same(first, second);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(500, first.Product!.PackageQuantity);
        Assert.Equal("g", first.Product.NutritionBasis);
        Assert.Equal(12.5m, first.Product.ProteinPer100);
        Assert.Null(first.Product.FatPer100);
        Assert.Null(first.Product.CarbsPer100);
        Assert.Contains("api/v3/product/3017620422003?fields=", handler.LastUri);
    }

    [Fact]
    public async Task MissingAndFailedResponses_AreCachedWithoutPretendingSuccess()
    {
        foreach (var status in new[] { HttpStatusCode.NotFound, HttpStatusCode.ServiceUnavailable })
        {
            using var handler = new Handler(_ => new(status));
            using var client = new OpenFoodFactsClient(new Factory(handler), TimeProvider.System);
            var first = await client.LookupAsync(Code, CancellationToken.None);
            Assert.Equal(status == HttpStatusCode.NotFound ? "not_found" : "unavailable", first.Status);
            Assert.Null(first.Product);
            await client.LookupAsync(Code, CancellationToken.None);
            Assert.Equal(1, handler.Calls);
        }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("{\"status\":\"success\",\"product\":{\"code\":\"036000291452\"}}")]
    [InlineData("{\"status\":\"success\",\"product\":null}")]
    public async Task MalformedOrMismatchedProducts_AreUnavailable(string payload)
    {
        using var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(payload) });
        using var client = new OpenFoodFactsClient(new Factory(handler), TimeProvider.System);
        Assert.Equal("unavailable", (await client.LookupAsync(Code, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task RateLimit_IsSharedAcrossCodesAndHonorsRetryAfter()
    {
        var clock = new Clock();
        using var handler = new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromMinutes(2));
            return response;
        });
        using var client = new OpenFoodFactsClient(new Factory(handler), clock);
        Assert.Equal("rate_limited", (await client.LookupAsync(Code, CancellationToken.None)).Status);
        Assert.Equal("rate_limited", (await client.LookupAsync("00000096385074", CancellationToken.None)).Status);
        Assert.Equal(1, handler.Calls);
        clock.Now = clock.Now.AddMinutes(3);
        await client.LookupAsync(Code, CancellationToken.None);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task OutboundBudget_StopsAtFifteenRequestsInSlidingMinute()
    {
        var clock = new Clock();
        using var handler = new Handler(_ => new(HttpStatusCode.NotFound));
        using var client = new OpenFoodFactsClient(new Factory(handler), clock);
        for (var index = 0; index < 15; index++)
        {
            await client.LookupAsync(index.ToString("D14"), CancellationToken.None);
        }
        Assert.Equal("rate_limited", (await client.LookupAsync(Code, CancellationToken.None)).Status);
        Assert.Equal(15, handler.Calls);
        clock.Now = clock.Now.AddMinutes(1);
        Assert.Equal("not_found", (await client.LookupAsync(Code, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        using var handler = new Handler(_ => throw new OperationCanceledException());
        using var client = new OpenFoodFactsClient(new Factory(handler), TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LookupAsync(Code, cancellation.Token));
    }

    [Fact]
    public void UnknownPackageUnit_DoesNotInventNutritionBasisOrPackageWeight()
    {
        using var document = JsonDocument.Parse("""{"status":"success","product":{"code":"3017620422003","quantity":"6 pieces","product_quantity":6,"product_quantity_unit":"pieces","nutriments":{"energy-kcal_100g":10}}}""");
        var result = OpenFoodFactsClient.Parse(document.RootElement, Code, DateTimeOffset.UtcNow);
        Assert.Equal(1, result.Product!.PackageQuantity);
        Assert.Equal("pcs", result.Product.Unit);
        Assert.Null(result.Product.CaloriesPer100);
        Assert.Null(result.Product.NutritionBasis);
    }

    [Fact]
    public void MissingPackageAmount_KeepsNutritionWhenItsBasisIsKnown()
    {
        using var document = JsonDocument.Parse("""{"status":"success","product":{"code":"3017620422003","product_quantity_unit":"g","nutriments":{"energy-kcal_100g":250}}}""");
        var result = OpenFoodFactsClient.Parse(document.RootElement, Code, DateTimeOffset.UtcNow);
        Assert.Equal(1, result.Product!.PackageQuantity);
        Assert.Equal("pcs", result.Product.Unit);
        Assert.Equal("g", result.Product.NutritionBasis);
        Assert.Equal(250, result.Product.CaloriesPer100);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri?.ToString();
            return Task.FromResult(respond(request));
        }
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false) { BaseAddress = new Uri("https://world.openfoodfacts.org/") };
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
