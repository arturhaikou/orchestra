using Microsoft.Extensions.Options;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Configuration;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Worker;

/// <summary>
/// Background worker that continuously polls for tickets eligible for agent execution
/// and orchestrates automated agent runs on those tickets.
/// </summary>
public class AgentExecutionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentExecutionWorker> _logger;
    private readonly AgentExecutionSettings _settings;

    public AgentExecutionWorker(
        IServiceProvider serviceProvider,
        IOptions<AgentExecutionSettings> settings,
        ILogger<AgentExecutionWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AgentExecutionWorker starting. Polling interval: {IntervalSeconds}s, Model: {Model}",
            _settings.PollingIntervalSeconds,
            _settings.ModelDeploymentName);

        // Wait briefly for DatabaseMigrationWorker to complete
        await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);

        _logger.LogInformation("AgentExecutionWorker starting polling loop");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEligibleTicketsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AgentExecutionWorker polling cycle");
            }

            // Wait for next polling interval
            await Task.Delay(
                TimeSpan.FromSeconds(_settings.PollingIntervalSeconds),
                stoppingToken);
        }

        _logger.LogInformation("AgentExecutionWorker stopped");
    }

    private async Task ProcessEligibleTicketsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var orchestrationService = scope.ServiceProvider
            .GetRequiredService<IAgentOrchestrationService>();
        var ticketDataAccess = scope.ServiceProvider
            .GetRequiredService<ITicketAgentExecutionDataAccess>();

        // Get eligible tickets
        var internalTickets = await ticketDataAccess
            .GetInternalTicketsReadyForAgentAsync(cancellationToken);
        var externalTickets = await ticketDataAccess
            .GetExternalMaterializedTicketsReadyForAgentAsync(cancellationToken);

        var allTickets = internalTickets.Concat(externalTickets).ToList();

        if (!allTickets.Any())
        {
            _logger.LogDebug("No tickets ready for agent execution");
            return;
        }

        _logger.LogInformation(
            "Found {Count} ticket(s) ready for agent execution ({InternalCount} internal, {ExternalCount} external)",
            allTickets.Count,
            internalTickets.Count,
            externalTickets.Count);

        // Execute agents for each ticket (concurrent execution allowed)
        var tasks = allTickets.Select(ticket =>
            ExecuteTicketAsync(orchestrationService, ticket.Id, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private async Task ExecuteTicketAsync(
        IAgentOrchestrationService orchestrationService,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await orchestrationService.ExecuteAgentForTicketAsync(
                ticketId,
                cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Successfully executed agent for ticket {TicketId}",
                    ticketId);
            }
            else
            {
                _logger.LogWarning(
                    "Agent execution failed for ticket {TicketId}: {Error}",
                    ticketId,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled error executing agent for ticket {TicketId}",
                ticketId);
        }
    }
}
