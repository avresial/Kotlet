using System.Net;
using System.Text.Json;
using Kotlet.Infrastructure.Products;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Kotlet.Infrastructure.UnitTests.Products;

public sealed class FoodDataCentralClientTests
{
    private const string Code = "00048707820026";
    private const string Search = """{"foods":[{"fdcId":2022017,"dataType":"Branded","gtinUpc":"048707820026"}]}""";
    private const string Detail = """{"fdcId":2022017,"dataType":"Branded","gtinUpc":"048707820026","description":"CHEDDAR","brandName":"Cheese brand","servingSize":30,"servingSizeUnit":"g","labelNutrients":{"calories":{"value":80.1},"protein":{"value":3},"carbohydrates":{"value":4},"fat":{"value":6}}}""";

    [Fact]
    public async Task ExactUpc_ConvertsServingAndCachesWithoutTreatingServingAsPackage()
    {
        using var handler = new Handler(Search, Detail);
        using var client = Client(handler);
        var result = await client.LookupAsync(Code, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(267, result.CaloriesPer100);
        Assert.Equal(10, result.ProteinPer100);
        Assert.Equal(13.333m, result.CarbsPer100);
        Assert.Equal(20, result.FatPer100);
        Assert.Equal("g", result.NutritionBasis);
        Assert.Equal("pcs", result.Unit);
        Assert.Equal(1, result.PackageQuantity);
        Assert.Equal(2022017, result.FoodDataCentralId);
        Assert.Contains("query=048707820026&", handler.Uris[0]);
        Assert.All(handler.Uris, uri => Assert.DoesNotContain("api_key", uri));
        Assert.Same(result, await client.LookupAsync(Code, CancellationToken.None));
        Assert.Equal(2, handler.Uris.Count);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("test", 2)]
    public async Task MissingKey_DisablesRequests(string key, int calls)
    {
        using var handler = new Handler(Search, Detail);
        using var client = Client(handler, key);
        await client.LookupAsync(Code, CancellationToken.None);
        Assert.Equal(calls, handler.Uris.Count);
    }

    [Theory]
    [InlineData("ml")]
    [InlineData("MLT")]
    public async Task Millilitres_UseLabelServingWithoutMixingMassNutrients(string unit)
    {
        var detail = Detail.Replace("\"g\"", $"\"{unit}\"").Replace("\"protein\":{\"value\":3},", "");
        detail = detail[..^1] + ",\"foodNutrients\":[{\"nutrient\":{\"id\":1003,\"unitName\":\"g\"},\"amount\":99}]}";
        using var handler = new Handler(Search, detail);
        using var client = Client(handler);
        var result = await client.LookupAsync(Code, CancellationToken.None);
        Assert.Equal("ml", result!.NutritionBasis);
        Assert.Equal(267, result.CaloriesPer100);
        Assert.Null(result.ProteinPer100);
    }

    [Theory]
    [InlineData("{\"foods\":[{\"fdcId\":1,\"dataType\":\"Branded\",\"gtinUpc\":\"3017620422003\"}]}", Detail, 2)]
    [InlineData(Search, "{\"fdcId\":2022017,\"dataType\":\"Branded\",\"gtinUpc\":\"3017620422003\"}", 2)]
    [InlineData("[]", Detail, 1)]
    [InlineData(Search, "not json", 2)]
    public async Task InvalidOrDifferentProduct_IsNotImported(string search, string detail, int calls)
    {
        using var handler = new Handler(search, detail);
        using var client = Client(handler);
        Assert.Null(await client.LookupAsync(Code, CancellationToken.None));
        Assert.Null(await client.LookupAsync(Code, CancellationToken.None));
        Assert.Equal(calls, handler.Uris.Count);
    }

    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        using var handler = new Handler(Search, Detail);
        using var client = Client(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LookupAsync(Code, cancellation.Token));
        Assert.Empty(handler.Uris);
    }


    [Fact]
    public async Task MissingUpcRepresentation_TriesEquivalentEanWithoutAcceptingAnotherProduct()
    {
        using var handler = new Handler("{\"foods\":[]}", Search, Detail);
        using var client = Client(handler);
        Assert.NotNull(await client.LookupAsync(Code, CancellationToken.None));
        Assert.Contains("query=048707820026&", handler.Uris[0]);
        Assert.Contains("query=0048707820026&", handler.Uris[1]);
        Assert.EndsWith("/food/2022017", handler.Uris[2]);
    }

    [Fact]
    public async Task SelectsHighestExactIdAndIgnoresAYoungerDifferentBarcode()
    {
        const string search = """{"foods":[{"fdcId":2022017,"dataType":"Branded","gtinUpc":"048707820026"},{"fdcId":9999999,"dataType":"Branded","gtinUpc":"3017620422003"},{"fdcId":2022019,"dataType":"Branded","gtinUpc":"0048707820026"}]}""";
        using var handler = new Handler(search, Detail.Replace("2022017", "2022019"));
        using var client = Client(handler);
        Assert.Equal(2022019, (await client.LookupAsync(Code, CancellationToken.None))!.FoodDataCentralId);
        Assert.EndsWith("/food/2022019", handler.Uris[1]);
    }

    [Theory]
    [InlineData("g")]
    [InlineData("GRM")]
    public void GramServing_ConvertsZeroAndLeavesInvalidOrAbsentValuesUnknown(string unit)
    {
        var payload = Detail.Replace("\"g\"", $"\"{unit}\"")
            .Replace("80.1", "0").Replace("\"value\":3", "\"value\":-1").Replace("\"value\":4", "\"value\":9999");
        using var document = JsonDocument.Parse(payload);
        var product = FoodDataCentralClient.ParseProduct(document.RootElement, Code, 2022017, DateTimeOffset.UtcNow);
        Assert.Equal(0, product!.CaloriesPer100);
        Assert.Null(product.ProteinPer100);
        Assert.Null(product.CarbsPer100);
        Assert.Equal(20, product.FatPer100);
    }

    [Fact]
    public void UnknownServingUnit_UsesOnlyExplicitPer100GramNutrientsWithCorrectUnits()
    {
        const string payload = """{"fdcId":2022017,"dataType":"Branded","gtinUpc":"048707820026","description":"Food","servingSize":2,"servingSizeUnit":"cups","labelNutrients":{"protein":{"value":99}},"foodNutrients":[{"nutrient":{"id":1003,"unitName":"g"},"amount":12},{"nutrient":{"id":1008,"unitName":"kJ"},"amount":400},{"nutrient":{"id":1005,"unitName":"g"},"amount":101},{"nutrient":{"id":1004,"unitName":"mg"},"amount":5}]}""";
        using var document = JsonDocument.Parse(payload);
        var product = FoodDataCentralClient.ParseProduct(document.RootElement, Code, 2022017, DateTimeOffset.UtcNow);
        Assert.Equal("g", product!.NutritionBasis);
        Assert.Equal(12, product.ProteinPer100);
        Assert.Null(product.CaloriesPer100);
        Assert.Null(product.CarbsPer100);
        Assert.Null(product.FatPer100);
    }

    [Theory]
    [InlineData("\"bad-id\"")]
    [InlineData("null")]
    [InlineData("-1")]
    public async Task MalformedIds_AreRejectedWithoutFetchingDetails(string id)
    {
        using var handler = new Handler(Search.Replace("2022017", id), Detail);
        using var client = Client(handler);
        Assert.Null(await client.LookupAsync(Code, CancellationToken.None));
        Assert.Equal(2, handler.Uris.Count);
    }

    [Fact]
    public async Task TooLargeResponse_IsBoundedAndCachedAsFailure()
    {
        using var handler = new Handler(new string(' ', 1_048_577));
        using var client = Client(handler);
        Assert.Null(await client.LookupAsync(Code, CancellationToken.None));
        Assert.Null(await client.LookupAsync(Code, CancellationToken.None));
        Assert.Single(handler.Uris);
    }

    [Fact]
    public async Task RateLimitCooldown_IsSharedAcrossCodesAndExpires()
    {
        var clock = new Clock();
        using var handler = new Handler(Search, Detail)
        {
            RespondAsync = (_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new(TimeSpan.FromMinutes(2));
                return Task.FromResult(response);
            }
        };
        using var client = new FoodDataCentralClient(new Factory(handler), Configuration("test"), clock);
        Assert.Null(await client.LookupAsync(Code, CancellationToken.None));
        Assert.Null(await client.LookupAsync("03017620422003", CancellationToken.None));
        Assert.Single(handler.Uris);
        clock.Now = clock.Now.AddMinutes(3);
        Assert.Null(await client.LookupAsync("00000096385074", CancellationToken.None));
        Assert.Equal(2, handler.Uris.Count);
    }

    [Fact]
    public async Task BudgetCountsSearchAndDetailRequests()
    {
        using var handler = new Handler(Search, Detail)
        {
            RespondAsync = (request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.Query.Contains("048707820026", StringComparison.Ordinal) ? Search
                : request.RequestUri.AbsolutePath.EndsWith("2022017", StringComparison.Ordinal) ? Detail : "{\"foods\":[]}")
            })
        };
        using var client = Client(handler);
        await client.LookupAsync(Code, CancellationToken.None); // Two outbound calls.
        for (var index = 1; index <= 20; index++)
        {
            var prefix = $"123456789{index:D3}";
            var sum = prefix.Select((digit, position) => (digit - '0') * (position % 2 == 0 ? 1 : 3)).Sum();
            var barcode = prefix + ((10 - sum % 10) % 10);
            Assert.Null(await client.LookupAsync(barcode, CancellationToken.None));
        }
        Assert.Equal(15, handler.Uris.Count);
    }

    [Fact]
    public async Task ActiveCallerCancellation_ReleasesGateAndDoesNotCacheTheCanceledLookup()
    {
        using var handler = new Handler(Search, Detail)
        {
            RespondAsync = async (_, token) => { await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.OK); }
        };
        using var client = Client(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LookupAsync(Code, cancellation.Token));
        handler.RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        Assert.Null(await client.LookupAsync(Code, CancellationToken.None));
        Assert.Equal(2, handler.Uris.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutAndServerFailure_AreCachedAndReturnNoInventedNutrition(bool timeout)
    {
        using var handler = new Handler(Search, Detail)
        {
            RespondAsync = (_, _) => timeout ? Task.FromException<HttpResponseMessage>(new OperationCanceledException())
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
        };
        using var client = Client(handler);
        Assert.Null(await client.LookupAsync(Code, CancellationToken.None));
        Assert.Null(await client.LookupAsync(Code, CancellationToken.None));
        Assert.Single(handler.Uris);
    }

    private static IConfiguration Configuration(string key) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["FoodDataCentral:ApiKey"] = key }).Build();

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static FoodDataCentralClient Client(Handler handler, string key = "test") => new(
        new Factory(handler), Configuration(key), TimeProvider.System);

    private sealed class Handler(params string[] payloads) : HttpMessageHandler
    {
        public List<string> Uris { get; } = [];
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? RespondAsync { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uris.Add(request.RequestUri!.ToString());
            Assert.Equal("test", request.Headers.GetValues("X-Api-Key").Single());
            return RespondAsync is not null ? RespondAsync(request, cancellationToken) : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(payloads[Math.Min(Uris.Count - 1, payloads.Length - 1)]) });
        }
    }

    private sealed class Factory(Handler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false)
        { BaseAddress = new Uri("https://api.nal.usda.gov/fdc/v1/") };
    }
}
