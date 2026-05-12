using Orchestra.Application.AiCliIntegrations.DTOs;
using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Application.AiCliIntegrations;

public sealed class AiCliIntegrationQueryService : IAiCliIntegrationQueryService
{
    private readonly IWorkspaceAuthorizationService _authService;
    private readonly IAiCliIntegrationDataAccess _dataAccess;

    public AiCliIntegrationQueryService(
        IWorkspaceAuthorizationService authService,
        IAiCliIntegrationDataAccess dataAccess)
    {
        _authService = authService;
        _dataAccess = dataAccess;
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

    private static AiCliIntegrationDto MapToDto(AiCliIntegration integration) =>
        new(
            Id: integration.Id,
            WorkspaceId: integration.WorkspaceId,
            Name: integration.Name,
            Provider: integration.Provider,
            UseLoggedInUser: integration.UseLoggedInUser,
            WorkingDirectory: integration.WorkingDirectory,
            ModelId: integration.ModelId,
            CliPath: integration.CliPath,
            CreatedAt: integration.CreatedAt,
            UpdatedAt: integration.UpdatedAt);
}
