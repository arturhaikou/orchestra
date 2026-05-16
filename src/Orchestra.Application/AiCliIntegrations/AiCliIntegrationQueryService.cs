using Orchestra.Application.AiCliIntegrations.DTOs;
using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Application.AiCliIntegrations;

public sealed class AiCliIntegrationQueryService : IAiCliIntegrationQueryService
{
    private readonly IWorkspaceAuthorizationService _authService;
    private readonly IAiCliIntegrationDataAccess _dataAccess;
    private readonly ICredentialEncryptionService _encryptionService;
    private readonly ICopilotModelDiscoveryService _modelDiscoveryService;

    public AiCliIntegrationQueryService(
        IWorkspaceAuthorizationService authService,
        IAiCliIntegrationDataAccess dataAccess,
        ICredentialEncryptionService encryptionService,
        ICopilotModelDiscoveryService modelDiscoveryService)
    {
        _authService = authService;
        _dataAccess = dataAccess;
        _encryptionService = encryptionService;
        _modelDiscoveryService = modelDiscoveryService;
    }

    public async Task<List<AiCliIntegrationDto>> GetListAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, workspaceId, cancellationToken);
        var integrations = await _dataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        return integrations.Select(MapToDto).ToList();
    }

    public async Task<AiCliIntegrationDto> GetByIdAsync(
        Guid userId,
        Guid workspaceId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, workspaceId, cancellationToken);
        var integration = await _dataAccess.GetByIdAsync(integrationId, cancellationToken)
            ?? throw new ArgumentException($"AI CLI integration '{integrationId}' was not found.", nameof(integrationId));

        if (integration.WorkspaceId != workspaceId)
            throw new WorkspaceAccessDeniedException(Guid.Empty, workspaceId,
                $"AI CLI integration '{integrationId}' does not belong to workspace '{workspaceId}'.");

        return MapToDto(integration);
    }

    public async Task<IReadOnlyList<ModelMetadataDto>> DiscoverModelsAsync(
        Guid userId,
        Guid workspaceId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, workspaceId, cancellationToken);

        var integration = await _dataAccess.GetByIdAsync(integrationId, cancellationToken)
            ?? throw new ArgumentException($"AI CLI integration '{integrationId}' was not found.", nameof(integrationId));

        if (integration.WorkspaceId != workspaceId)
            throw new WorkspaceAccessDeniedException(Guid.Empty, workspaceId,
                $"AI CLI integration '{integrationId}' does not belong to workspace '{workspaceId}'.");

        var credential = integration.EncryptedCredential is not null
            ? _encryptionService.Decrypt(integration.EncryptedCredential)
            : null;

        return await _modelDiscoveryService.DiscoverModelsAsync(
            credential,
            integration.UseLoggedInUser,
            integration.WorkingDirectory,
            integration.CliPath,
            cancellationToken);
    }

    private static AiCliIntegrationDto MapToDto(AiCliIntegration integration) =>
        new(
            Id: integration.Id,
            WorkspaceId: integration.WorkspaceId,
            Name: integration.Name,
            Provider: integration.Provider,
            UseLoggedInUser: integration.UseLoggedInUser,
            WorkingDirectory: integration.WorkingDirectory,
            CliPath: integration.CliPath,
            CreatedAt: integration.CreatedAt,
            UpdatedAt: integration.UpdatedAt);
}

