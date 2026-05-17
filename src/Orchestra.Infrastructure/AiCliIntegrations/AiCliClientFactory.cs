using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Infrastructure.AiCliIntegrations;

public sealed class AiCliClientFactory : IAiCliClientFactory
{
    private readonly IAiCliIntegrationDataAccess _dataAccess;
    private readonly ICredentialEncryptionService _encryptionService;

    public AiCliClientFactory(
        IAiCliIntegrationDataAccess dataAccess,
        ICredentialEncryptionService encryptionService)
    {
        _dataAccess = dataAccess;
        _encryptionService = encryptionService;
    }

    public async Task<IAiCliClient> CreateClientAsync(
        Guid integrationId,
        string? modelId = null,
        string? reasoningEffort = null,
        CancellationToken cancellationToken = default)
    {
        var (integration, credential) = await LoadIntegrationAsync(integrationId, cancellationToken);

        return integration.Provider switch
        {
            AiCliProviderType.GITHUB_COPILOT =>
                await GithubCopilotCliClient.CreateAsync(
                    credential,
                    integration.UseLoggedInUser,
                    integration.WorkingDirectory,
                    modelId,
                    integration.CliPath,
                    reasoningEffort,
                    cancellationToken),

            _ => throw new NotSupportedException(
                $"AI CLI provider '{integration.Provider}' is not yet implemented.")
        };
    }

    public async Task<IAiCliClient> CreateReadOnlyClientAsync(
        Guid integrationId,
        string? modelId = null,
        string? reasoningEffort = null,
        CancellationToken cancellationToken = default)
    {
        var (integration, credential) = await LoadIntegrationAsync(integrationId, cancellationToken);

        return integration.Provider switch
        {
            AiCliProviderType.GITHUB_COPILOT =>
                await GithubCopilotCliClient.CreateReadOnlyAsync(
                    credential,
                    integration.UseLoggedInUser,
                    integration.WorkingDirectory,
                    modelId,
                    integration.CliPath,
                    reasoningEffort,
                    cancellationToken),

            _ => throw new NotSupportedException(
                $"AI CLI provider '{integration.Provider}' does not support read-only mode.")
        };
    }

    private async Task<(Orchestra.Domain.Entities.AiCliIntegration integration, string? credential)> LoadIntegrationAsync(
        Guid integrationId,
        CancellationToken cancellationToken)
    {
        var integration = await _dataAccess.GetByIdAsync(integrationId, cancellationToken)
            ?? throw new ArgumentException($"AI CLI integration '{integrationId}' was not found.", nameof(integrationId));

        var credential = integration.EncryptedCredential is not null
            ? _encryptionService.Decrypt(integration.EncryptedCredential)
            : null;

        return (integration, credential);
    }
}
