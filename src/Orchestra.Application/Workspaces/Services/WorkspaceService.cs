using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Workspaces.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceDataAccess _workspaceDataAccess;
    private readonly IWorkspaceProviderService _workspaceProviderService;
    private readonly IWorkspaceAIProviderRepository _aiProviderRepository;

    public WorkspaceService(
        IWorkspaceDataAccess workspaceDataAccess,
        IWorkspaceProviderService workspaceProviderService,
        IWorkspaceAIProviderRepository aiProviderRepository)
    {
        _workspaceDataAccess = workspaceDataAccess;
        _workspaceProviderService = workspaceProviderService;
        _aiProviderRepository = aiProviderRepository;
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(Guid userId, CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        // --- Step 1: Validate provider config fields before any entity construction or I/O ---
        ValidateProviderConfigFields(request);

        // --- Step 2: Construct workspace entity (assigns a new Guid Id internally) ---
        var workspace = Workspace.Create(
            request.Name,
            userId,
            request.AiSummarizationModelId,
            request.CustomerSatisfactionAnalysisModelId,
            aiProviderType: request.ProviderType);

        // Apply AI feature flags if provided
        if (request.IsAiSummarizationEnabled.HasValue || request.IsCustomerSatisfactionAnalysisEnabled.HasValue)
        {
            workspace.UpdateAiSettings(
                request.IsAiSummarizationEnabled ?? false,
                request.IsCustomerSatisfactionAnalysisEnabled ?? false);
        }

        // --- Step 3: Stage the provider configuration (no SaveChangesAsync inside) ---
        await _workspaceProviderService.CreateProviderConfigAsync(
            workspace.Id,
            request.ProviderType!.Value,
            request.Endpoint,
            request.ApiKey,
            request.DefaultModelId,
            cancellationToken);

        // --- Step 4: Atomically persist workspace + provider config + membership in one SaveChangesAsync ---
        await _workspaceDataAccess.CreateAsync(workspace, cancellationToken);

        return new WorkspaceDto(
            workspace.Id.ToString(),
            workspace.Name,
            workspace.IsAiSummarizationEnabled,
            workspace.IsCustomerSatisfactionAnalysisEnabled,
            workspace.AiSummarizationModelId,
            workspace.CustomerSatisfactionAnalysisModelId,
            request.DefaultModelId,
            workspace.OwnerId.ToString());
    }

    /// <summary>
    /// Validates that the provider config fields in the request are consistent with the chosen provider type.
    /// Throws <see cref="ArgumentException"/> (mapped to 400 Bad Request) on any violation.
    /// All validation occurs before any entity construction or database write.
    /// </summary>
    private static void ValidateProviderConfigFields(CreateWorkspaceRequest request)
    {
        if (request.ProviderType is null)
        {
            throw new ArgumentException(
                "ProviderType is required. Specify 'AzureOpenAI' or 'Ollama'.",
                nameof(request.ProviderType));
        }

        if (request.ProviderType == AIProviderType.AzureOpenAI)
        {
            if (string.IsNullOrWhiteSpace(request.Endpoint))
            {
                throw new ArgumentException(
                    "Endpoint is required when ProviderType is AzureOpenAI.",
                    nameof(request.Endpoint));
            }

            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                throw new ArgumentException(
                    "ApiKey is required when ProviderType is AzureOpenAI.",
                    nameof(request.ApiKey));
            }
        }

        if (request.ProviderType == AIProviderType.Ollama)
        {
            if (string.IsNullOrWhiteSpace(request.Endpoint))
            {
                throw new ArgumentException(
                    "Endpoint is required when ProviderType is Ollama.",
                    nameof(request.Endpoint));
            }

            if (!string.IsNullOrWhiteSpace(request.ApiKey))
            {
                throw new ArgumentException(
                    "ApiKey must be absent or null when ProviderType is Ollama.",
                    nameof(request.ApiKey));
            }

            if (string.IsNullOrWhiteSpace(request.DefaultModelId))
            {
                throw new ArgumentException(
                    "defaultModelId is required when ProviderType is Ollama.",
                    nameof(request.DefaultModelId));
            }
        }
    }

    public async Task<WorkspaceDto[]> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var workspaces = await _workspaceDataAccess.GetUserWorkspacesAsync(userId, cancellationToken);
        
        var dtos = new List<WorkspaceDto>(workspaces.Count);
        foreach (var w in workspaces)
        {
            var aiConfig = await _aiProviderRepository.GetByWorkspaceIdAsync(w.Id, cancellationToken);
            dtos.Add(new WorkspaceDto(
                w.Id.ToString(),
                w.Name,
                w.IsAiSummarizationEnabled,
                w.IsCustomerSatisfactionAnalysisEnabled,
                w.AiSummarizationModelId,
                w.CustomerSatisfactionAnalysisModelId,
                aiConfig?.DefaultModelId,
                w.OwnerId.ToString()));
        }
        return dtos.ToArray();
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
        
        // Return DTO with model IDs — DefaultModelId sourced from AIProviderConfiguration
        var aiConfig = await _aiProviderRepository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        return new WorkspaceDto(
            workspace.Id.ToString(), 
            workspace.Name, 
            workspace.IsAiSummarizationEnabled, 
            workspace.IsCustomerSatisfactionAnalysisEnabled,
            workspace.AiSummarizationModelId,
            workspace.CustomerSatisfactionAnalysisModelId,
            aiConfig?.DefaultModelId,
            workspace.OwnerId.ToString());
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