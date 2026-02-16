using Orchestra.Infrastructure;
using Orchestra.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.AddInfrastructureServices();

// Register database migration worker
builder.Services.AddHostedService<DatabaseMigrationWorker>();

// Register agent execution worker
builder.Services.AddHostedService<AgentExecutionWorker>();

var host = builder.Build();

await host.RunAsync();
