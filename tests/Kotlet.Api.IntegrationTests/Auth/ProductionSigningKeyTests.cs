using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Auth;

/// <summary>
/// Guards the OpenIddict key setup that only runs outside Development and Test, which the rest of
/// the suite never reaches because every other host here runs as "Test".
/// </summary>
public sealed class ProductionSigningKeyTests
{
    /// <summary>
    /// OpenIddict builds its server options lazily, on the first request rather than at startup, so
    /// a server registering only symmetric signing keys starts up looking healthy and then fails
    /// every request with a 500 — including the refresh call the SPA blocks its first paint on.
    /// </summary>
    [Fact]
    public async Task Requests_are_served_when_the_host_runs_outside_development()
    {
        using var factory = new ProductionHost();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ingredients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Signing_credentials_include_an_asymmetric_key()
    {
        using var factory = new ProductionHost();

        Assert.Contains(SigningKeys(factory), key => key is AsymmetricSecurityKey);
    }

    /// <summary>
    /// The point of deriving the keys instead of generating them: the hosting plan stops the site
    /// when idle, and a key that changes on restart invalidates every token issued before it.
    /// </summary>
    [Fact]
    public void Signing_key_is_the_same_after_a_restart()
    {
        using var first = new ProductionHost();
        using var second = new ProductionHost();

        Assert.Equal(KeyIds(first), KeyIds(second));
    }

    private static IReadOnlyList<SecurityKey> SigningKeys(ProductionHost host) =>
        host.Services.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>()
            .CurrentValue.SigningCredentials.Select(credentials => credentials.Key).ToList();

    private static IReadOnlyList<string> KeyIds(ProductionHost host) =>
        SigningKeys(host).Select(key => Base64UrlEncoder.Encode(key.ComputeJwkThumbprint())).Order().ToList();

    private sealed class ProductionHost : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("Jwt:Issuer", "Kotlet.Tests");
            builder.UseSetting("Jwt:Audience", "Kotlet.Tests");
            builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-bytes");
            // HTTPS throughout: the validation that runs outside development rejects plain HTTP.
            builder.UseSetting("OAuth:Issuer", "https://kotlet.test/");
            builder.UseSetting("OAuth:Resource", "https://kotlet.test/mcp");
            builder.UseSetting("OAuth:LoginUrl", "https://kotlet.test/login");
            builder.UseSetting("OAuth:ClientId", "kotlet-mcp-tests");
            builder.UseSetting("OAuth:RequirePkce", "true");
            builder.UseSetting("OAuth:RedirectUris:0", "https://kotlet.test/callback");
            builder.UseEnvironment("Production");
        }
    }
}
