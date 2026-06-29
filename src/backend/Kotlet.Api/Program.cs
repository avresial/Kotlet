using Kotlet.Application.MealPlanner;
using Kotlet.Application.Ai;
using Kotlet.Api.Ai;
using Kotlet.Application.Menu.GetMenu;
using Kotlet.Application.Ingredients;
using Kotlet.Api.Auth;
using Kotlet.Api.Admin;
using Kotlet.Api.Houses;
using Kotlet.Api.Ingredients;
using Kotlet.Api.Localization;
using Kotlet.Api.Mcp;
using Kotlet.Api.MealPlanner;
using Kotlet.Api.Persistence;
using Kotlet.Application.Pantry;
using Kotlet.Api.Pantry;
using Kotlet.Application.Recipes;
using Kotlet.Api.Recipes;
using Kotlet.Application.Shopping;
using Kotlet.Api.Shopping;
using Kotlet.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Kotlet.Infrastructure.Persistence;
using Kotlet.Domain.Auth;
using Scalar.AspNetCore;
using OpenIddict.Abstractions;
using ModelContextProtocol.AspNetCore.Authentication;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;
using System.Text;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<GetMenuQueryHandler>();
builder.Services.AddScoped<UserAiProviderService>();
builder.Services.AddScoped<IngredientService>();
builder.Services.AddScoped<PantryService>();
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<RecipeImageService>();
builder.Services.AddScoped<MealPlannerService>();
builder.Services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateOnStart();
builder.Services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName).ValidateOnStart();
builder.Services.AddOptions<OAuthOptions>().BindConfiguration(OAuthOptions.SectionName).ValidateOnStart();
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
var oauth = builder.Configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>()
    ?? throw new InvalidOperationException("OAuth configuration is missing.");
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
var allowHttp = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test");
if (!Uri.TryCreate(oauth.Issuer, UriKind.Absolute, out var oauthIssuer) ||
    !Uri.TryCreate(oauth.Resource, UriKind.Absolute, out var oauthResource) ||
    !Uri.TryCreate(oauth.LoginUrl, UriKind.Absolute, out var oauthLogin) ||
    (!allowHttp && (oauthIssuer.Scheme != Uri.UriSchemeHttps || oauthResource.Scheme != Uri.UriSchemeHttps)))
    throw new InvalidOperationException("OAuth issuer, resource and login URL must be absolute; issuer and resource require HTTPS outside development.");
if (oauthLogin.Scheme != Uri.UriSchemeHttps && (!allowHttp || oauthLogin.Scheme != Uri.UriSchemeHttp || !oauthLogin.IsLoopback))
    throw new InvalidOperationException("OAuth login URL must use HTTPS or loopback HTTP in development.");
if (oauth.RedirectUris.Length == 0 || oauth.RedirectUris.Any(value =>
        !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttps && (uri.Scheme != Uri.UriSchemeHttp || !uri.IsLoopback))))
    throw new InvalidOperationException("OAuth redirect URIs must use HTTPS or loopback HTTP.");
builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<KotletDbContext>())
    .AddServer(options =>
    {
        options.SetIssuer(oauthIssuer)
            .SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RegisterScopes("mcp")
            .RegisterResources(oauth.Resource)
            .DisableAccessTokenEncryption();
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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
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
                if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId, context.HttpContext.RequestAborted))
                    context.Fail("The user no longer exists.");
            }
        };
    })
    .AddPolicyScheme("McpOAuth", "MCP OAuth", options =>
    {
        options.ForwardAuthenticate = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        options.ForwardChallenge = McpAuthenticationDefaults.AuthenticationScheme;
        options.ForwardForbid = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    })
    .AddMcp(options => options.ResourceMetadata = new()
    {
        AuthorizationServers = { oauth.Issuer },
        ScopesSupported = ["mcp"]
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(RoleNames.Admin, policy => policy.RequireRole(RoleNames.Admin));
    options.AddPolicy("Mcp", policy =>
    {
        policy.AddAuthenticationSchemes("McpOAuth");
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => context.User.HasScope("mcp"));
    });
});
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<IdentityTools>()
    .WithTools<KotletReadTools>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ILanguageContext, LanguageContext>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<MigrationReadySignal>();
builder.Services.AddHostedService<DatabaseMigrationWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Kotlet API";
        options.Theme = ScalarTheme.Purple;
    });
}

app.MapDefaultEndpoints();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapOAuthEndpoints();
app.MapMcp("/mcp").RequireAuthorization("Mcp");
app.MapAiProviderEndpoints();
app.MapHouseEndpoints();
app.MapAdminEndpoints();
app.MapIngredientEndpoints();
app.MapPantryEndpoints();
app.MapShoppingListEndpoints();
app.MapRecipeEndpoints();
app.MapMealPlannerEndpoints();
app.MapGet("/api/menu", async (GetMenuQueryHandler handler, CancellationToken cancellationToken) =>
    Results.Ok(await handler.Handle(new GetMenuQuery(), cancellationToken)))
    .WithName("GetMenu");

app.Run();

// Exposes the implicit Program class so integration tests can reference it via WebApplicationFactory<Program>.
public partial class Program;
