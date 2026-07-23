using Kotlet.Api;
using Kotlet.Application;
using Kotlet.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapApiFeatures();

app.Run();

// Exposes the implicit Program class so integration tests can reference it via WebApplicationFactory<Program>.
public partial class Program;
