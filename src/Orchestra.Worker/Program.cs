using Orchestra.Infrastructure;
using Orchestra.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// Register SignalR with Redis backplane so Worker can publish events
// that the API hub broadcasts to connected clients.
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("redis")!);

builder.AddInfrastructureServices();

// Register database migration worker
builder.Services.AddHostedService<DatabaseMigrationWorker>();

// Register agent execution worker
builder.Services.AddHostedService<AgentExecutionWorker>();

var host = builder.Build();

await host.RunAsync();
