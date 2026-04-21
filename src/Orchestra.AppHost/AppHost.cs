using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("Orchestra");

var adfgenerator = builder.AddNodeApp("adfgenerator", "../../apps/adfgenerator", "index.js")
    .WithHttpEndpoint(port: 3300, env: "PORT");

var worker = builder.AddProject<Projects.Orchestra_Worker>("worker")
    .WithReference(database)
    .WithReference(adfgenerator)
    .WaitFor(database);

var api = builder.AddProject<Projects.Orchestra_ApiService>("api")
    .WithReference(database)
    .WithReference(adfgenerator)
    .WaitFor(database)
    .WaitFor(worker);

var ui = builder.AddViteApp("ui", "../../apps/ui")
    .WithReference(api)
    .WaitFor(api);

// Inject the UI's dynamically-assigned HTTP endpoint into the API CORS configuration
api.WithEnvironment("Cors__AllowedOrigins__0", ui.GetEndpoint("http"));

builder.Build().Run();
