namespace Orchestra.Application.Workflows.Interfaces;

public interface IWorkflowExecutionEngine
{
    Task StartWorkflowAsync(Guid ticketId, Guid workflowDefinitionId, CancellationToken cancellationToken = default);
    Task HandleJobCompletedAsync(Guid jobId, string? output, CancellationToken cancellationToken = default);
    Task HandleJobWaitingForInputAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task HandleJobResumedAsync(Guid jobId, CancellationToken cancellationToken = default);
}
