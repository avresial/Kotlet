using System.Security.Cryptography;

var builder = DistributedApplication.CreateBuilder(args);

var databaseProvider = builder.Configuration["Database:Provider"] ?? "PostgreSQL";
var jwtSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var api = builder.AddProject<Projects.Kotlet_Api>("api")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);

if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    api.WithEnvironment("Database__Provider", "Sqlite");
}
else if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    var database = builder.AddConnectionString("kotletdb");
    api.WithReference(database); 
}
else
{
    throw new InvalidOperationException(
        $"Unsupported database provider '{databaseProvider}'. Use 'PostgreSQL' or 'Sqlite'.");
}

builder.AddExecutable("web", "npm", "../../frontend", "start", "--", "--host", "0.0.0.0", "--port", "4200")
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
