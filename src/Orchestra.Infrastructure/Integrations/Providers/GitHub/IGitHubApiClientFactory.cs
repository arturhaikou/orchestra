using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub;

public interface IGitHubApiClientFactory
{
    IGitHubApiClient CreateClient(Integration integration);
}
