using System.Net;
using Kotlet.Api.Recipes;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Mcp;

public sealed class RecipeImageFileDownloaderTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("100.64.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("192.0.0.8")]
    [InlineData("192.0.2.1")]
    [InlineData("192.88.99.2")]
    [InlineData("198.18.0.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("2002:7f00:1::")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("2001::1")]
    [InlineData("2001:db8::1")]
    [InlineData("3fff::1")]
    public void Destination_RejectsNonPublicAndTransitionAddresses(string address) =>
        Assert.False(RecipeImageFileDownloader.IsPublicAddress(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2001:4860:4860::8888")]
    public void Destination_AcceptsPublicAddresses(string address) =>
        Assert.True(RecipeImageFileDownloader.IsPublicAddress(IPAddress.Parse(address)));

    [Fact]
    public async Task EmptyAllowlist_FailsClosedWithoutSendingRequest()
    {
        using var handler = new RecipeImageDownloadHandler();
        using var client = new HttpClient(handler);
        var downloader = new RecipeImageFileDownloader(client, Options.Create(new RecipeImageFileOptions()));

        var error = await Assert.ThrowsAsync<McpException>(() => downloader.DownloadAsync(
            new("https://images.example.test/png", "file-id"), CancellationToken.None));

        Assert.Contains("not allowed", error.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ProductionHandler_DisablesAmbientAccessAndRejectsLoopbackDns()
    {
        using var handler = RecipeImageFileDownloader.CreateHandler();
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.False(handler.UseCookies);
        Assert.Null(handler.Credentials);
        Assert.Null(handler.ActivityHeadersPropagator);
        Assert.NotNull(handler.ConnectCallback);
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://localhost/"));
        Assert.Contains("public addresses", error.ToString());
    }

    [Fact]
    public async Task CallerCancellation_IsPreserved()
    {
        using var handler = new RecipeImageDownloadHandler();
        using var client = new HttpClient(handler);
        var downloader = new RecipeImageFileDownloader(client,
            Options.Create(new RecipeImageFileOptions { AllowedDownloadHosts = ["images.example.test"] }));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync(
            new("https://images.example.test/png", "file-id"), cancellation.Token));
    }
}
