using Orchestra.Domain.Entities;

namespace Orchestra.Application.Workflows.Interfaces;

public interface IWorkflowExecutionRepository
{
    Task<WorkflowExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkflowExecution?> GetActiveByTicketIdAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<List<WorkflowExecution>> GetByTicketIdAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task AddAsync(WorkflowExecution execution, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkflowExecution execution, CancellationToken cancellationToken = default);
    Task<List<WorkflowStepExecution>> GetStepExecutionsByExecutionIdAsync(Guid workflowExecutionId, CancellationToken cancellationToken = default);
    Task<WorkflowStepExecution?> GetStepExecutionByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task AddStepExecutionAsync(WorkflowStepExecution stepExecution, CancellationToken cancellationToken = default);
    Task UpdateStepExecutionAsync(WorkflowStepExecution stepExecution, CancellationToken cancellationToken = default);
}
