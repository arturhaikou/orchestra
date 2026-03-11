using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Application.Common.Exceptions;

namespace Orchestra.Application.Workspaces.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceDataAccess _workspaceDataAccess;
    private readonly IWorkspaceAIModelValidationService _aiModelValidationService;

    public WorkspaceService(
        IWorkspaceDataAccess workspaceDataAccess,
        IWorkspaceAIModelValidationService aiModelValidationService)
    {
        _workspaceDataAccess = workspaceDataAccess;
        _aiModelValidationService = aiModelValidationService;
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(Guid userId, CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        // Validate AI model identifiers before creating the workspace
        await _aiModelValidationService.ValidateAIModelIdentifiersAsync(
            request.AiSummarizationModelId,
            request.CustomerSatisfactionAnalysisModelId,
            cancellationToken);

        var workspace = Workspace.Create(
            request.Name, 
            userId,
            request.AiSummarizationModelId,
            request.CustomerSatisfactionAnalysisModelId);

        // Apply AI settings if provided in the request
        if (request.IsAiSummarizationEnabled.HasValue || request.IsCustomerSatisfactionAnalysisEnabled.HasValue)
        {
            workspace.UpdateAiSettings(
                request.IsAiSummarizationEnabled ?? false,
                request.IsCustomerSatisfactionAnalysisEnabled ?? false);
        }

        await _workspaceDataAccess.CreateAsync(workspace, cancellationToken);

        return new WorkspaceDto(
            workspace.Id.ToString(), 
            workspace.Name, 
            workspace.IsAiSummarizationEnabled, 
            workspace.IsCustomerSatisfactionAnalysisEnabled,
            workspace.AiSummarizationModelId,
            workspace.CustomerSatisfactionAnalysisModelId);
    }

    public async Task<WorkspaceDto[]> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var workspaces = await _workspaceDataAccess.GetUserWorkspacesAsync(userId, cancellationToken);
        
        return workspaces
            .Select(w => new WorkspaceDto(
                w.Id.ToString(), 
                w.Name, 
                w.IsAiSummarizationEnabled, 
                w.IsCustomerSatisfactionAnalysisEnabled,
                w.AiSummarizationModelId,
                w.CustomerSatisfactionAnalysisModelId))
            .ToArray();
    }

    public async Task<WorkspaceDto> UpdateWorkspaceAsync(
        Guid userId,
        Guid workspaceId,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        // Retrieve workspace
        var workspace = await _workspaceDataAccess.GetByIdAsync(
            workspaceId, 
            cancellationToken);
        
        if (workspace is null)
        {
            throw new WorkspaceNotFoundException(workspaceId);
        }
        
        // Check membership first (security: don't leak existence to non-members)
        var isMember = await _workspaceDataAccess.IsUserMemberAsync(workspaceId, userId, cancellationToken);
        if (!isMember)
        {
            // Return 404 to non-members (don't leak workspace existence)
            throw new WorkspaceNotFoundException(workspaceId);
        }
        
        // Verify ownership
        if (workspace.OwnerId != userId)
        {
            throw new UnauthorizedWorkspaceAccessException(
                "You do not have permission to update this workspace", 
                userId, 
                workspaceId);
        }
        
        // Validate AI model identifiers before mutating the workspace
        await _aiModelValidationService.ValidateAIModelIdentifiersAsync(
            request.AiSummarizationModelId,
            request.CustomerSatisfactionAnalysisModelId,
            cancellationToken);
        
        // Update using domain method (validates and sets UpdatedAt)
        workspace.UpdateName(request.Name);

        // Determine if model IDs should be updated (partial-update semantics)
        // If either AI setting or model ID field is present, we apply the update
        bool shouldUpdateAiSettings = request.IsAiSummarizationEnabled.HasValue || 
                                      request.IsCustomerSatisfactionAnalysisEnabled.HasValue;
        bool shouldUpdateModelIds = request.AiSummarizationModelId != null || 
                                    request.CustomerSatisfactionAnalysisModelId != null;
        
        if (shouldUpdateAiSettings || shouldUpdateModelIds)
        {
            workspace.UpdateAiSettings(
                request.IsAiSummarizationEnabled ?? workspace.IsAiSummarizationEnabled,
                request.IsCustomerSatisfactionAnalysisEnabled ?? workspace.IsCustomerSatisfactionAnalysisEnabled,
                request.AiSummarizationModelId,
                request.CustomerSatisfactionAnalysisModelId,
                updateModelIds: shouldUpdateModelIds);
        }

        // Persist changes
        await _workspaceDataAccess.UpdateAsync(workspace, cancellationToken);
        
        // Return DTO with model IDs
        return new WorkspaceDto(
            workspace.Id.ToString(), 
            workspace.Name, 
            workspace.IsAiSummarizationEnabled, 
            workspace.IsCustomerSatisfactionAnalysisEnabled,
            workspace.AiSummarizationModelId,
            workspace.CustomerSatisfactionAnalysisModelId);
    }

    public async Task DeleteWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceDataAccess.GetByIdAsync(workspaceId, cancellationToken);

        if (workspace == null)
        {
            throw new WorkspaceNotFoundException(workspaceId);
        }

        // Check membership first (security: don't leak existence to non-members)
        var isMember = await _workspaceDataAccess.IsUserMemberAsync(workspaceId, userId, cancellationToken);
        if (!isMember)
        {
            // Return 404 to non-members (don't leak workspace existence)
            throw new WorkspaceNotFoundException(workspaceId);
        }

        if (workspace.OwnerId != userId)
        {
            throw new UnauthorizedWorkspaceAccessException(
                "You do not have permission to delete this workspace", 
                userId, 
                workspaceId);
        }

        await _workspaceDataAccess.DeleteAsync(workspaceId, cancellationToken);
    }
}