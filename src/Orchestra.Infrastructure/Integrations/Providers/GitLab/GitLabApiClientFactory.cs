using Microsoft.Extensions.Logging;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab;

public class GitLabApiClientFactory : IGitLabApiClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitLabApiClient> _logger;
    private readonly ICredentialEncryptionService _credentialEncryptionService;

    public GitLabApiClientFactory(
        IHttpClientFactory httpClientFactory,
        ILogger<GitLabApiClient> logger,
        ICredentialEncryptionService credentialEncryptionService)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _credentialEncryptionService = credentialEncryptionService;
    }

    public IGitLabApiClient CreateClient(Integration integration)
    {
        if (string.IsNullOrEmpty(integration.Url))
            throw new InvalidOperationException("GitLab integration URL is required. Expected format: https://gitlab.com/{namespace}/{project} or https://your-instance.com/{namespace}/{project}");

        var uri = new Uri(integration.Url);

        // API base = scheme + host (supports both gitlab.com and self-hosted instances)
        var apiBaseUrl = $"{uri.Scheme}://{uri.Host}";

        // Project path = everything after the host, stripped of leading slash
        // e.g. https://gitlab.com/myorg/myrepo  → "myorg/myrepo"
        // e.g. https://gl.acme.com/group/sub/repo → "group/sub/repo"
        var projectPath = uri.AbsolutePath.Trim('/');

        if (string.IsNullOrEmpty(projectPath) || !projectPath.Contains('/'))
            throw new InvalidOperationException("Invalid GitLab URL format. Expected: https://gitlab.com/{namespace}/{project}");

        if (string.IsNullOrEmpty(integration.EncryptedApiKey))
            throw new InvalidOperationException("GitLab API token is required");

        var apiToken = _credentialEncryptionService.Decrypt(integration.EncryptedApiKey);
        var httpClient = _httpClientFactory.CreateClient();

        return new GitLabApiClient(httpClient, apiBaseUrl, projectPath, apiToken, _logger);
    }
}
