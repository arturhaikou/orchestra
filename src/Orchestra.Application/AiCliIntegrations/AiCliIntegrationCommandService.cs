using Orchestra.Application.AiCliIntegrations.DTOs;
using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Application.AiCliIntegrations;

public sealed class AiCliIntegrationCommandService : IAiCliIntegrationCommandService
{
    private readonly IWorkspaceAuthorizationService _authService;
    private readonly IAiCliIntegrationDataAccess _dataAccess;
    private readonly ICredentialEncryptionService _encryptionService;

    public AiCliIntegrationCommandService(
        IWorkspaceAuthorizationService authService,
        IAiCliIntegrationDataAccess dataAccess,
        ICredentialEncryptionService encryptionService)
    {
        _authService = authService;
        _dataAccess = dataAccess;
        _encryptionService = encryptionService;
    }

    public async Task<AiCliIntegrationDto> CreateAsync(
        Guid userId,
        CreateAiCliIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, request.WorkspaceId, cancellationToken);
        await EnsureNameIsUniqueAsync(request.WorkspaceId, request.Name, excludeId: null, cancellationToken);

        var encryptedCredential = request.Credential is not null
            ? _encryptionService.Encrypt(request.Credential)
            : null;

        var integration = AiCliIntegration.Create(
            request.WorkspaceId,
            request.Name,
            request.Provider,
            encryptedCredential,
            request.UseLoggedInUser,
            request.WorkingDirectory,
            request.ModelId,
            request.CliPath);

        await _dataAccess.AddAsync(integration, cancellationToken);
        return MapToDto(integration);
    }

    public async Task<AiCliIntegrationDto> UpdateAsync(
        Guid userId,
        Guid integrationId,
        UpdateAiCliIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, request.WorkspaceId, cancellationToken);
        var integration = await LoadInWorkspaceAsync(integrationId, request.WorkspaceId, cancellationToken);
        await EnsureNameIsUniqueAsync(request.WorkspaceId, request.Name, excludeId: integrationId, cancellationToken);

        var encryptedCredential = request.UseLoggedInUser
            ? null
            : request.Credential is not null
                ? _encryptionService.Encrypt(request.Credential)
                : integration.EncryptedCredential;

        integration.Update(request.Name, encryptedCredential, request.UseLoggedInUser, request.WorkingDirectory, request.ModelId, request.CliPath);
        await _dataAccess.UpdateAsync(integration, cancellationToken);
        return MapToDto(integration);
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid integrationId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _authService.ValidateMembershipAsync(userId, workspaceId, cancellationToken);
        var integration = await LoadInWorkspaceAsync(integrationId, workspaceId, cancellationToken);
        await _dataAccess.DeleteAsync(integration, cancellationToken);
    }

    private async Task EnsureNameIsUniqueAsync(
        Guid workspaceId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var exists = await _dataAccess.ExistsByNameAsync(workspaceId, name, excludeId, cancellationToken);
        if (exists)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["name"] = [$"An AI CLI integration named '{name}' already exists in this workspace."]
            });
    }

    private async Task<AiCliIntegration> LoadInWorkspaceAsync(
        Guid integrationId, Guid workspaceId, CancellationToken cancellationToken)
    {
        var integration = await _dataAccess.GetByIdAsync(integrationId, cancellationToken)
            ?? throw new ArgumentException($"AI CLI integration '{integrationId}' was not found.", nameof(integrationId));

        if (integration.WorkspaceId != workspaceId)
            throw new WorkspaceAccessDeniedException(Guid.Empty, workspaceId,
                $"AI CLI integration '{integrationId}' does not belong to workspace '{workspaceId}'.");

        return integration;
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
