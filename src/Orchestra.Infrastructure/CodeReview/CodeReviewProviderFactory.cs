using Orchestra.Application.CodeReview;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.CodeReview.Providers;
using Orchestra.Infrastructure.Integrations.Providers.GitHub;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;

namespace Orchestra.Infrastructure.CodeReview;

public class CodeReviewProviderFactory : ICodeReviewProviderFactory
{
    private readonly IGitHubApiClientFactory _gitHubApiClientFactory;
    private readonly IGitLabApiClientFactory _gitLabApiClientFactory;

    public CodeReviewProviderFactory(
        IGitHubApiClientFactory gitHubApiClientFactory,
        IGitLabApiClientFactory gitLabApiClientFactory)
    {
        _gitHubApiClientFactory = gitHubApiClientFactory;
        _gitLabApiClientFactory = gitLabApiClientFactory;
    }

    public ICodeReviewProvider Create(Integration integration)
    {
        return integration.Provider switch
        {
            ProviderType.GITHUB => new GitHubReviewProvider(
                _gitHubApiClientFactory.CreateClient(integration)),
            ProviderType.GITLAB => new GitLabReviewProvider(
                _gitLabApiClientFactory.CreateClient(integration)),
            _ => throw new NotSupportedException(
                $"Code review not supported for provider: {integration.Provider}")
        };
    }
}
