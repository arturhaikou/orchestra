using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab;

public interface IGitLabApiClientFactory
{
    IGitLabApiClient CreateClient(Integration integration);
}
