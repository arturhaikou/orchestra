namespace Orchestra.Application.AiCliIntegrations.Interfaces;

public interface ICopilotModelDiscoveryService
{
    Task<IReadOnlyList<string>> DiscoverModelsAsync(
        string? credential,
        bool useLoggedInUser,
        string workingDirectory,
        string? cliPath = null,
        CancellationToken cancellationToken = default);
}
