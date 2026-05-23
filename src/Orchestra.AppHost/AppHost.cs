using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("Orchestra");

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

var adfgenerator = builder.AddNodeApp("adfgenerator", "../../apps/adfgenerator", "index.js")
    .WithHttpEndpoint(port: 3300, env: "PORT");

var worker = builder.AddProject<Projects.Orchestra_Worker>("worker")
    .WithReference(database)
    .WithReference(redis)
    .WithReference(adfgenerator)
    .WaitFor(database)
    .WaitFor(redis);

var api = builder.AddProject<Projects.Orchestra_ApiService>("api")
    .WithReference(database)
    .WithReference(redis)
    .WithReference(adfgenerator)
    .WaitFor(database)
    .WaitFor(worker)
    .WaitFor(redis);

var copilotruntime = builder.AddNodeApp("copilotruntime", "../../apps/copilotkit-runtime", "index.js")
    .WithHttpEndpoint(port: 3001, env: "PORT")
    .WithReference(api)
    .WaitFor(api);

var ui = builder.AddViteApp("ui", "../../apps/ui")
    .WithReference(api)
    .WithReference(copilotruntime)
    .WaitFor(api)
    .WaitFor(copilotruntime);

// Inject the UI's dynamically-assigned HTTP endpoint into the API CORS configuration
api.WithEnvironment("Cors__AllowedOrigins__0", ui.GetEndpoint("http"));

// Inject the runtime's dynamically-assigned HTTP endpoint into the API CORS configuration
api.WithEnvironment("Cors__AllowedOrigins__1", copilotruntime.GetEndpoint("http"));

// Inject the UI origin into the CopilotKit runtime for CORS
copilotruntime.WithEnvironment("CORS_ORIGIN", ui.GetEndpoint("http"));

builder.Build().Run();
