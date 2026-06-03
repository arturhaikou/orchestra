using Orchestra.Domain.Entities;

namespace Orchestra.Application.Workflows.Interfaces;

public interface IWorkflowDefinitionRepository
{
    Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<WorkflowDefinition>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<List<WorkflowStep>> GetStepsByDefinitionIdAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default);
    Task AddAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReplaceStepsAsync(Guid workflowDefinitionId, List<WorkflowStep> steps, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, List<string>>> GetSystemToolsByDefinitionIdAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default);
    Task ReplaceStepSystemToolsAsync(Guid stepId, List<string> toolIdentifiers, CancellationToken cancellationToken = default);
}
