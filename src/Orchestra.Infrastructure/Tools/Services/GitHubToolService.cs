namespace Orchestra.Infrastructure.Tools.Services;

public class GitHubToolService : IGitHubToolService
{
    public GitHubToolService()
    {
        // Constructor for future DI dependencies (IHttpClientFactory, ICredentialEncryptionService, ILogger)
    }

    public Task<object> GetPullRequestAsync()
    {
        throw new NotImplementedException("This tool method is not yet implemented");
    }

    public Task<object> GetIssueAsync()
    {
        throw new NotImplementedException("This tool method is not yet implemented");
    }
}