using Orchestra.Infrastructure;
using Orchestra.Infrastructure.Agents;
using Orchestra.Worker;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// Register SignalR with Redis backplane so Worker can publish events
// that the API hub broadcasts to connected clients.
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("redis")!)
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redis")!));

builder.AddInfrastructureServices();

// Register database migration worker
builder.Services.AddHostedService<DatabaseMigrationWorker>();

// Subscribe to cross-process job cancellation signals published by the API
builder.Services.AddHostedService<Orchestra.Infrastructure.Jobs.JobCancellationSubscriber>();

// Register agent execution worker
builder.Services.AddHostedService<AgentExecutionWorker>();

// Register job resumption and session recovery services
builder.Services.AddHostedService<JobResumeCoordinator>();
builder.Services.AddHostedService<AgentSessionRecoveryService>();

var host = builder.Build();

await host.RunAsync();
