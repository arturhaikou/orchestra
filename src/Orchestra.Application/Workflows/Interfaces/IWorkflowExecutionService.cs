using Orchestra.Application.Workflows.DTOs;

namespace Orchestra.Application.Workflows.Interfaces;

public interface IWorkflowExecutionService
{
    Task<WorkflowExecutionDto?> GetByIdAsync(Guid executionId, CancellationToken cancellationToken = default);
    Task<List<WorkflowExecutionDto>> GetByTicketIdAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
