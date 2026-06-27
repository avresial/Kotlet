using Kotlet.Application.Menu.GetMenu;
using Kotlet.Api.Auth;
using Kotlet.Api.Persistence;
using Kotlet.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<GetMenuQueryHandler>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "kotlet.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
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
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapGet("/api/menu", async (GetMenuQueryHandler handler, CancellationToken cancellationToken) =>
    Results.Ok(await handler.Handle(new GetMenuQuery(), cancellationToken)))
    .WithName("GetMenu");

app.Run();
