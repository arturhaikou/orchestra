using GitHub.Copilot.SDK;
using Orchestra.Application.AiCliIntegrations.Interfaces;

namespace Orchestra.Infrastructure.AiCliIntegrations;

public sealed class CopilotModelDiscoveryService : ICopilotModelDiscoveryService
{
    public async Task<IReadOnlyList<string>> DiscoverModelsAsync(
        string? credential,
        bool useLoggedInUser,
        string workingDirectory,
        string? cliPath = null,
        CancellationToken cancellationToken = default)
    {
        await using var client = new CopilotClient(new CopilotClientOptions
        {
            GitHubToken = useLoggedInUser ? null : credential,
            UseLoggedInUser = useLoggedInUser,
            Cwd = workingDirectory,
            CliPath = cliPath
        });

        await client.StartAsync(cancellationToken);

        var models = await client.ListModelsAsync(cancellationToken);

        return models.Select(m => m.Id).Where(id => id is not null).ToList()!;
    }
}
