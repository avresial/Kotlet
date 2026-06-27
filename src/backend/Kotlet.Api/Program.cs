using Kotlet.Application.Menu.GetMenu;
using Kotlet.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure();
builder.Services.AddScoped<GetMenuQueryHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapGet("/api/menu", async (GetMenuQueryHandler handler, CancellationToken cancellationToken) =>
    Results.Ok(await handler.Handle(new GetMenuQuery(), cancellationToken)))
    .WithName("GetMenu");

app.Run();

// Exposes the implicit Program class so integration tests can reference it via WebApplicationFactory<Program>.
public partial class Program;
