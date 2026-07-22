using System.Security.Claims;
using System.Text;
using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Kotlet.Api.Auth;

public static class DiExtension
{
    public static IServiceCollection AddAuthFeature(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateOnStart();
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName).ValidateOnStart();
        services.AddOptions<OAuthOptions>().BindConfiguration(OAuthOptions.SectionName).ValidateOnStart();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");
        var oauth = configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>()
            ?? throw new InvalidOperationException("OAuth configuration is missing.");
        Validate(jwt, oauth, environment);

        var allowHttp = environment.IsDevelopment() || environment.IsEnvironment("Test");
        var oauthIssuer = new Uri(oauth.Issuer);
        // RFC 7591 Dynamic Client Registration endpoint. AI agents (Claude Code, Claude
        // Desktop, claude.ai) refuse to connect to an MCP server whose discovery metadata
        // lacks a registration_endpoint: they register their own client (with their own
        // loopback / callback redirect URI) rather than using a pre-shared client id the way
        // ChatGPT does. The endpoint itself is served by OAuthEndpoints.MapOAuthEndpoints.
        var registrationEndpoint = new Uri(oauthIssuer, "connect/register").AbsoluteUri;
        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<KotletDbContext>())
            .AddServer(options =>
            {
                options.SetIssuer(oauthIssuer)
                    // Serve the authorization-server metadata on both the OpenID Connect and the
                    // RFC 8414 well-known paths. MCP clients probe /.well-known/oauth-authorization-server
                    // first (some only probe that), so advertising both maximises compatibility.
                    .SetConfigurationEndpointUris(
                        "/.well-known/openid-configuration",
                        "/.well-known/oauth-authorization-server")
                    .SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow()
                    .RegisterScopes("mcp")
                    .RegisterResources(oauth.Resource)
                    .DisableAccessTokenEncryption();
                // Advertise the DCR endpoint in the discovery document. OpenIddict does not host a
                // registration endpoint itself, so it is added to the metadata by hand.
                options.AddEventHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>(builder =>
                    builder.UseInlineHandler(context =>
                    {
                        context.Metadata[OpenIddictConstants.Metadata.RegistrationEndpoint] = registrationEndpoint;
                        return default;
                    }));
                if (allowHttp)
                    options.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();
                else
                    // ponytail: ephemeral keys avoid Azure certificate-store writes; use a persistent certificate when sessions must survive restarts.
                    options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
                var aspNetCore = options.UseAspNetCore().EnableAuthorizationEndpointPassthrough();
                if (allowHttp)
                    aspNetCore.DisableTransportSecurityRequirement();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (!Guid.TryParse(subject, out var userId))
                        {
                            context.Fail("The token subject is invalid.");
                            return;
                        }

                        var db = context.HttpContext.RequestServices.GetRequiredService<KotletDbContext>();
                        if (!await db.Users.AsNoTracking().AnyAsync(
                                user => user.Id == userId,
                                context.HttpContext.RequestAborted))
                            context.Fail("The user no longer exists.");
                    }
                };
            });
        services.AddAuthorization(options =>
            options.AddPolicy(RoleNames.Admin, policy => policy.RequireRole(RoleNames.Admin)));
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<TokenService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAuthFeature(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthEndpoints();
        endpoints.MapOAuthEndpoints();
        endpoints.MapOAuthRegistrationEndpoints();
        return endpoints;
    }

    private static void Validate(JwtOptions jwt, OAuthOptions oauth, IWebHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");

        var allowHttp = environment.IsDevelopment() || environment.IsEnvironment("Test");
        if (!Uri.TryCreate(oauth.Issuer, UriKind.Absolute, out var issuer) ||
            !Uri.TryCreate(oauth.Resource, UriKind.Absolute, out var resource) ||
            !Uri.TryCreate(oauth.LoginUrl, UriKind.Absolute, out var login) ||
            (!allowHttp && (issuer.Scheme != Uri.UriSchemeHttps || resource.Scheme != Uri.UriSchemeHttps)))
            throw new InvalidOperationException(
                "OAuth issuer, resource and login URL must be absolute; issuer and resource require HTTPS outside development.");
        if (login.Scheme != Uri.UriSchemeHttps &&
            (!allowHttp || login.Scheme != Uri.UriSchemeHttp || !login.IsLoopback))
            throw new InvalidOperationException("OAuth login URL must use HTTPS or loopback HTTP in development.");
        if (oauth.RedirectUris.Length == 0 || oauth.RedirectUris.Any(value =>
                !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && (uri.Scheme != Uri.UriSchemeHttp || !uri.IsLoopback))))
            throw new InvalidOperationException("OAuth redirect URIs must use HTTPS or loopback HTTP.");
    }
}
