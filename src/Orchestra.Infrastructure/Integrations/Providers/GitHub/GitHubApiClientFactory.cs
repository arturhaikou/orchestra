using Microsoft.Extensions.Logging;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub;

public class GitHubApiClientFactory : IGitHubApiClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubApiClient> _logger;
    private readonly ICredentialEncryptionService _credentialEncryptionService;

    public GitHubApiClientFactory(IHttpClientFactory httpClientFactory, ILogger<GitHubApiClient> logger, ICredentialEncryptionService credentialEncryptionService)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _credentialEncryptionService = credentialEncryptionService;
    }

    public IGitHubApiClient CreateClient(Integration integration)
    {
        if (string.IsNullOrEmpty(integration.Url))
            throw new InvalidOperationException("GitHub integration URL is required. Expected format: https://github.com/{owner}/{repo}");

        // Extract owner and repo from URL
        // URL format should be: https://github.com/owner/repo
        var uri = new Uri(integration.Url);
        var segments = uri.AbsolutePath.Trim('/').Split('/');

        if (segments.Length < 2)
            throw new InvalidOperationException("Invalid GitHub repository URL format. Expected: https://github.com/{owner}/{repo}");

        var owner = segments[0];
        var repo = segments[1];

        if (string.IsNullOrEmpty(integration.EncryptedApiKey))
            throw new InvalidOperationException("GitHub API token is required");

        var apiToken = _credentialEncryptionService.Decrypt(integration.EncryptedApiKey);
        var httpClient = _httpClientFactory.CreateClient();

        return new GitHubApiClient(httpClient, owner, repo, apiToken, _logger);
    }
}
