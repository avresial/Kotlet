var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("kotletdb");

var api = builder.AddProject<Projects.Kotlet_Api>("api")
    .WithReference(database)
    .WaitFor(database);

builder.AddExecutable("web", "npm", "../../frontend", "start", "--", "--host", "0.0.0.0", "--port", "4200")
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
