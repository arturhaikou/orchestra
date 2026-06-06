namespace Orchestra.Application.Workflows.Interfaces;

public interface IWorkflowExecutionEngine
{
    Task StartWorkflowAsync(Guid ticketId, Guid workflowDefinitionId, CancellationToken cancellationToken = default);
    Task HandleJobCompletedAsync(Guid jobId, string? output, CancellationToken cancellationToken = default);
    Task HandleJobWaitingForInputAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task HandleJobResumedAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running workflow execution. Returns false if not found or already terminal.
    /// </summary>
    Task<bool> CancelWorkflowAsync(Guid workflowExecutionId, CancellationToken cancellationToken = default);
}
