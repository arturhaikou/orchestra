using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Infrastructure.Tools.WorkflowTools;

namespace Orchestra.Infrastructure.Workflows;

public class WorkflowSystemToolRegistry : IWorkflowSystemToolRegistry
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;

    public WorkflowSystemToolRegistry(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
    }

    public IReadOnlyList<string> AvailableTools { get; } = ["switch_workflow_ticket"];

    public AIFunction? Create(string toolIdentifier, JobTrackingContext jobTracking)
    {
        return toolIdentifier switch
        {
            "switch_workflow_ticket" => SwitchWorkflowTicketFunction.Create(
                jobTracking,
                _scopeFactory,
                _loggerFactory.CreateLogger<WorkflowSystemToolRegistry>()),
            _ => null
        };
    }
}
