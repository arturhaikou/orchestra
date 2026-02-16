var builder = DistributedApplication.CreateBuilder(args);

var openai = builder.AddConnectionString("openai");
var modelName = builder.AddParameter("model");

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("Orchestra");

var adfgenerator = builder.AddNodeApp("adfgenerator", "../../apps/adfgenerator", "index.js")
    .WithHttpEndpoint(port: 3300, env: "PORT");

var worker = builder.AddProject<Projects.Orchestra_Worker>("worker")
    .WithReference(database)
    .WithReference(adfgenerator)
    .WithReference(openai)
    .WithEnvironment("AgentExecution__ModelDeploymentName", modelName)
    .WaitFor(database);

var api = builder.AddProject<Projects.Orchestra_ApiService>("api")
    .WithReference(database)
    .WithReference(adfgenerator)
    .WithReference(openai)
    .WithEnvironment("AgentExecution__ModelDeploymentName", modelName)
    .WaitFor(database)
    .WaitFor(worker);

builder.AddViteApp("ui", "../../apps/ui")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
