using Kotlet.Application.Menu.GetMenu;
using Kotlet.Application.Ingredients;
using Kotlet.Api.Auth;
using Kotlet.Api.Ingredients;
using Kotlet.Api.Persistence;
using Kotlet.Application.Pantry;
using Kotlet.Api.Pantry;
using Kotlet.Application.Shopping;
using Kotlet.Api.Shopping;
using Kotlet.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<GetMenuQueryHandler>();
builder.Services.AddScoped<IngredientService>();
builder.Services.AddScoped<PantryService>();
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateOnStart();
builder.Services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName).ValidateOnStart();
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
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
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });
builder.Services.AddAuthorization();
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
builder.Services.AddScoped<TokenService>();
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
app.MapIngredientEndpoints();
app.MapPantryEndpoints();
app.MapShoppingListEndpoints();
app.MapGet("/api/menu", async (GetMenuQueryHandler handler, CancellationToken cancellationToken) =>
    Results.Ok(await handler.Handle(new GetMenuQuery(), cancellationToken)))
    .WithName("GetMenu");

app.Run();

// Exposes the implicit Program class so integration tests can reference it via WebApplicationFactory<Program>.
public partial class Program;
