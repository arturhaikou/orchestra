using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var aiProviderParam = builder.AddParameter("aiProvider");
var modelParam = builder.AddParameter("model");

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("Orchestra");

var adfgenerator = builder.AddNodeApp("adfgenerator", "../../apps/adfgenerator", "index.js")
    .WithHttpEndpoint(port: 3300, env: "PORT");

// ── AI provider branching ──────────────────────────────────────────────────
// Read the raw parameter value at host-build time to select the correct
// Aspire resource type. The parameter value is also forwarded to Worker and
// API as AgentExecution__Provider so the Infrastructure layer can branch at
// DI registration time.
var aiProvider = builder.Configuration["Parameters:aiProvider"] ?? "Azure";
var modelName = builder.Configuration["Parameters:model"] ?? string.Empty;

IResourceBuilder<IResourceWithConnectionString> aiResource;
IResourceBuilder<OllamaResource>? ollamaContainer = null;

if (aiProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
{
    // Ollama path: spin up a managed container, pull all configured models,
    // and persist the model cache across AppHost restarts via a data volume.
    var ollama = builder.AddOllama("ollama")
        .WithDataVolume();

    // Pull every model in the configured list, excluding the default model
    // (which is registered separately via AddModel("ai", modelName))
    var extraModels = builder.Configuration.GetSection("Ollama:Models")
                             .Get<string[]>() ?? [];
    foreach (var m in extraModels.Where(
                 m => !m.Equals(modelName, StringComparison.OrdinalIgnoreCase)))
    {
        ollama.AddModel(m, m);
    }

    // Register the default model with connection-string key "ai"
    ollamaContainer = ollama;
    aiResource = ollama.AddModel("ai", modelName);
}
else
{
    // Azure path (default): external connection string — not a managed
    // container. The value comes from the host environment / user secrets.
    aiResource = builder.AddConnectionString("ai");
}
// ──────────────────────────────────────────────────────────────────────────

var worker = builder.AddProject<Projects.Orchestra_Worker>("worker")
    .WithReference(database)
    .WithReference(adfgenerator)
    .WithReference(aiResource)
    .WithEnvironment("AgentExecution__Provider", aiProvider)
    .WithEnvironment("AgentExecution__ModelDeploymentName", modelName)
    .WaitFor(aiResource)
    .WaitFor(database);

var api = builder.AddProject<Projects.Orchestra_ApiService>("api")
    .WithReference(database)
    .WithReference(adfgenerator)
    .WithReference(aiResource)
    .WithEnvironment("AgentExecution__Provider", aiProvider)
    .WithEnvironment("AgentExecution__ModelDeploymentName", modelName)
    .WaitFor(aiResource)
    .WaitFor(database)
    .WaitFor(worker);

// ── Provider-specific environment variable injection ──────────────────────
if (ollamaContainer is not null)
{
    // Ollama path: inject base URL and indexed model names
    var ollamaEndpoint = ollamaContainer.GetEndpoint("http");

    worker = worker.WithEnvironment("Ollama__BaseUrl", ollamaEndpoint);
    api = api.WithEnvironment("Ollama__BaseUrl", ollamaEndpoint);

    // Inject all configured models as indexed env vars
    var allModels = builder.Configuration.GetSection("Ollama:Models")
                           .Get<string[]>() ?? [modelName];
    for (int i = 0; i < allModels.Length; i++)
    {
        worker = worker.WithEnvironment($"Ollama__Models__{i}", allModels[i]);
        api = api.WithEnvironment($"Ollama__Models__{i}", allModels[i]);
    }
}
else
{
    // Azure path: inject Azure model deployment names as indexed env vars
    var azureModels = builder.Configuration.GetSection("AzureAI:Models")
                             .Get<string[]>() ?? [];
    for (int i = 0; i < azureModels.Length; i++)
    {
        worker = worker.WithEnvironment($"AzureAI__Models__{i}", azureModels[i]);
        api = api.WithEnvironment($"AzureAI__Models__{i}", azureModels[i]);
    }
}
// ──────────────────────────────────────────────────────────────────────────

builder.AddViteApp("ui", "../../apps/ui")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
