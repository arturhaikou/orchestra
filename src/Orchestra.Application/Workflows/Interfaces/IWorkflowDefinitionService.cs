using Orchestra.Application.Workflows.DTOs;

namespace Orchestra.Application.Workflows.Interfaces;

public interface IWorkflowDefinitionService
{
    Task<WorkflowDefinitionDto> CreateAsync(Guid userId, CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionDto> UpdateAsync(Guid userId, Guid workflowId, UpdateWorkflowDefinitionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid workflowId, CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionDto?> GetByIdAsync(Guid userId, Guid workflowId, CancellationToken cancellationToken = default);
    Task<List<WorkflowDefinitionDto>> GetByWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);
}
