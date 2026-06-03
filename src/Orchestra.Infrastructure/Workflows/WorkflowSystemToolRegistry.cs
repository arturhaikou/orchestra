using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Infrastructure.Tools.WorkflowTools;

namespace Orchestra.Infrastructure.Workflows;

public class WorkflowSystemToolRegistry : IWorkflowSystemToolRegistry
{
    private readonly IWorkflowExecutionRepository _executionRepository;
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly INotificationService _notificationService;
    private readonly ILoggerFactory _loggerFactory;

    public WorkflowSystemToolRegistry(
        IWorkflowExecutionRepository executionRepository,
        ITicketDataAccess ticketDataAccess,
        INotificationService notificationService,
        ILoggerFactory loggerFactory)
    {
        _executionRepository = executionRepository;
        _ticketDataAccess = ticketDataAccess;
        _notificationService = notificationService;
        _loggerFactory = loggerFactory;
    }

    public IReadOnlyList<string> AvailableTools { get; } = ["switch_workflow_ticket"];

    public AIFunction? Create(string toolIdentifier, JobTrackingContext jobTracking)
    {
        return toolIdentifier switch
        {
            "switch_workflow_ticket" => SwitchWorkflowTicketFunction.Create(
                jobTracking,
                _executionRepository,
                _ticketDataAccess,
                _notificationService,
                _loggerFactory.CreateLogger<WorkflowSystemToolRegistry>()),
            _ => null
        };
    }
}
