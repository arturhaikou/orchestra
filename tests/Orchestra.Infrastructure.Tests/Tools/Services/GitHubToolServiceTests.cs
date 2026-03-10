using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Infrastructure.Integrations.Providers.GitHub;
using Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;
using Orchestra.Infrastructure.Tools.Attributes;
using Orchestra.Infrastructure.Tools.Services;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Infrastructure.Tests.Tools.Services;

public class GitHubToolServiceTests : ServiceTestFixture<GitHubToolService>
{
    private readonly Guid _testWorkspaceId = new("12345678-1234-1234-1234-123456789012");
    private readonly IGitHubApiClientFactory _apiClientFactory = Substitute.For<IGitHubApiClientFactory>();
    private readonly IGitHubApiClient _apiClient = Substitute.For<IGitHubApiClient>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly GitHubToolService _sut;

    public GitHubToolServiceTests()
    {
        _apiClientFactory.CreateClient(Arg.Any<Integration>()).Returns(_apiClient);
        _sut = new GitHubToolService(_apiClientFactory, _integrationDataAccess, Logger);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsIssueData_WhenIntegrationExistsAndApiSucceeds()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var issue = new GitHubIssue
        {
            Number = 42,
            Title = "Fix bug",
            Body = "Details here",
            State = "open",
            HtmlUrl = "https://github.com/myorg/myrepo/issues/42",
            Assignee = new GitHubUser { Login = "alice" },
            Labels = new List<GitHubLabel> { new() { Name = "bug" } }
        };
        _apiClient.GetIssueAsync(42, Arg.Any<CancellationToken>()).Returns(issue);

        // Act
        var result = await _sut.GetIssueAsync(workspaceId, "42");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Number);
        Assert.Equal("Fix bug", result.Title);
        Assert.Equal("Details here", result.Body);
        Assert.Equal("open", result.State);
        Assert.Equal("https://github.com/myorg/myrepo/issues/42", result.Url);
        Assert.NotNull(result.Assignees);
        Assert.Contains("alice", result.Assignees);
        Assert.NotNull(result.Labels);
        Assert.Contains("bug", result.Labels);
    }

    [Fact]
    public async Task GetPullRequestAsync_ReturnsPrData_WhenIntegrationExistsAndApiSucceeds()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var pr = new GitHubPullRequest
        {
            Number = 17,
            Title = "Add feature",
            Body = "PR body",
            State = "open",
            Merged = false,
            HtmlUrl = "https://github.com/myorg/myrepo/pull/17",
            Head = new GitHubBranch { Ref = "feature/my-branch" },
            Base = new GitHubBranch { Ref = "main" },
            Mergeable = true
        };
        _apiClient.GetPullRequestAsync(17, Arg.Any<CancellationToken>()).Returns(pr);

        // Act
        var result = await _sut.GetPullRequestAsync(workspaceId, "17");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(17, result.Number);
        Assert.Equal("Add feature", result.Title);
        Assert.False(result.Merged);
        Assert.Equal("feature/my-branch", result.HeadBranch);
        Assert.Equal("main", result.BaseBranch);
        Assert.True(result.Mergeable);
    }

    [Fact]
    public async Task SearchIssuesAsync_ReturnsSearchResults_WhenIntegrationExistsAndApiSucceeds()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var searchResult = new GitHubSearchResult
        {
            TotalCount = 2,
            Items = new List<GitHubIssue>
            {
                new() { Number = 1, Title = "Issue A", State = "open", HtmlUrl = "https://github.com/myorg/myrepo/issues/1" },
                new() { Number = 2, Title = "Issue B", State = "closed", HtmlUrl = "https://github.com/myorg/myrepo/issues/2" }
            }
        };
        _apiClient.SearchIssuesAsync("login bug", 10, Arg.Any<CancellationToken>()).Returns(searchResult);

        // Act
        var result = await _sut.SearchIssuesAsync(workspaceId, "login bug");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items?.Length);
    }

    [Fact]
    public async Task SearchIssuesAsync_ClampsLimitTo30_WhenLimitExceedsMax()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });
        _apiClient.SearchIssuesAsync(Arg.Any<string>(), 30, Arg.Any<CancellationToken>())
            .Returns(new GitHubSearchResult());

        // Act
        await _sut.SearchIssuesAsync(workspaceId, "bug", limit: 100);

        // Assert — API client was called with limit clamped to 30
        await _apiClient.Received(1).SearchIssuesAsync(Arg.Any<string>(), 30, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsError_WhenNoGitHubIntegrationExists()
    {
        // Arrange
        var workspaceId = Guid.NewGuid().ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Integration>());

        // Act
        var result = await _sut.GetIssueAsync(workspaceId, "1");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No active GitHub integration", result.Error);
    }

    [Fact]
    public async Task GetPullRequestAsync_ReturnsError_WhenNoGitHubIntegrationExists()
    {
        _integrationDataAccess.GetByWorkspaceIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Integration>());

        var result = await _sut.GetPullRequestAsync(Guid.NewGuid().ToString(), "1");

        Assert.False(result.Success);
        Assert.Contains("No active GitHub integration", result.Error);
    }

    [Fact]
    public async Task SearchIssuesAsync_ReturnsError_WhenNoGitHubIntegrationExists()
    {
        _integrationDataAccess.GetByWorkspaceIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Integration>());

        var result = await _sut.SearchIssuesAsync(Guid.NewGuid().ToString(), "bug");

        Assert.False(result.Success);
        Assert.Contains("No active GitHub integration", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsAuthError_WhenApiThrowsAuthException()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });
        _apiClient.GetIssueAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(new InvalidOperationException("Failed to authenticate with GitHub. Please verify the API key.")));

        // Act
        var result = await _sut.GetIssueAsync(integration.WorkspaceId.ToString(), "1");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Failed to authenticate with GitHub. Please verify the API key.", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsNotFoundError_WhenApiThrows404Exception()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });
        _apiClient.GetIssueAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(new InvalidOperationException("GitHub repository not found or insufficient permissions.")));

        // Act
        var result = await _sut.GetIssueAsync(integration.WorkspaceId.ToString(), "999");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("GitHub repository not found or insufficient permissions.", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsRateLimitError_WhenApiThrowsRateLimitException()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });
        _apiClient.GetIssueAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(new InvalidOperationException("GitHub API rate limit exceeded. Please try again later.")));

        // Act
        var result = await _sut.GetIssueAsync(integration.WorkspaceId.ToString(), "1");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("GitHub API rate limit exceeded. Please try again later.", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsConnectivityError_WhenSocketExceptionThrown()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });
        var socketException = new SocketException();
        var httpException = new HttpRequestException("Network failure", socketException);
        _apiClient.GetIssueAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.GetIssueAsync(integration.WorkspaceId.ToString(), "1");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Unable to reach GitHub API. Please check connectivity.", result.Error);
    }

    [Fact]
    public void IGitHubToolService_HasToolCategoryAttribute()
    {
        // Arrange & Act
        var attribute = typeof(IGitHubToolService)
            .GetCustomAttribute<ToolCategoryAttribute>();

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal("GitHub", attribute!.Name);
        Assert.Equal(ProviderType.GITHUB, attribute.ProviderType);
    }

    [Fact]
    public void IGitHubToolService_HasToolActionAttributeForGetIssue()
    {
        var method = typeof(IGitHubToolService).GetMethod("GetIssueAsync");
        var attribute = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("get_issue", attribute!.Name);
        Assert.Equal(DangerLevel.Safe, attribute.DangerLevel);
    }

    [Fact]
    public void IGitHubToolService_HasToolActionAttributeForGetPr()
    {
        var method = typeof(IGitHubToolService).GetMethod("GetPullRequestAsync");
        var attribute = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("get_pr", attribute!.Name);
        Assert.Equal(DangerLevel.Safe, attribute.DangerLevel);
    }

    [Fact]
    public void IGitHubToolService_HasToolActionAttributeForSearchIssues()
    {
        var method = typeof(IGitHubToolService).GetMethod("SearchIssuesAsync");
        var attribute = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("search_issues", attribute!.Name);
        Assert.Equal(DangerLevel.Safe, attribute.DangerLevel);
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsSuccessResult_WhenIntegrationExistsAndApiSucceeds()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        var integration = IntegrationBuilder.GitHubIntegration();
        var title = "Test Issue";
        var body = "This is a test issue body.";

        var issue = new GitHubIssue
        {
            Number = 42,
            Title = title,
            HtmlUrl = "https://github.com/myorg/myrepo/issues/42"
        };

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        _apiClient.CreateIssueAsync(title, body, null, Arg.Any<CancellationToken>())
            .Returns(issue);

        // Act
        var result = await _sut.CreateIssueAsync(workspaceId, title, body);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Number);
        Assert.Equal("Test Issue", result.Title);
        Assert.Equal("https://github.com/myorg/myrepo/issues/42", result.Url);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenNoGitHubIntegrationExists()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration>());

        // Act
        var result = await _sut.CreateIssueAsync(workspaceId, "Title", "Body");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No active GitHub integration", result.Error);
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenAuthenticationFails()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var httpException = new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);
        _apiClient.CreateIssueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.CreateIssueAsync(workspaceId, "Title", "Body");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to authenticate", result.Error);
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenRepositoryNotFound()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var httpException = new HttpRequestException("Not Found", null, System.Net.HttpStatusCode.NotFound);
        _apiClient.CreateIssueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.CreateIssueAsync(workspaceId, "Title", "Body");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("repository not found", result.Error);
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenRateLimitExceeded()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var httpException = new HttpRequestException("Too Many Requests", null, (System.Net.HttpStatusCode)429);
        _apiClient.CreateIssueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.CreateIssueAsync(workspaceId, "Title", "Body");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("rate limit exceeded", result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsSuccessResult_WhenTitleAndBodyProvided()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        var integration = IntegrationBuilder.GitHubIntegration();
        var issueNumber = "42";
        var newTitle = "Updated Title";
        var newBody = "Updated body text.";

        var issue = new GitHubIssue
        {
            Number = 42,
            Title = newTitle,
            HtmlUrl = "https://github.com/myorg/myrepo/issues/42"
        };

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        _apiClient.UpdateIssueAsync(42, newTitle, newBody, Arg.Any<CancellationToken>())
            .Returns(issue);

        // Act
        var result = await _sut.UpdateIssueAsync(workspaceId, issueNumber, newTitle, newBody);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Number);
        Assert.Equal(newTitle, result.Title);
        Assert.Equal("https://github.com/myorg/myrepo/issues/42", result.Url);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsSuccessResult_WhenOnlyTitleProvided()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        var integration = IntegrationBuilder.GitHubIntegration();
        var issueNumber = "42";
        var newTitle = "Updated Title";

        var issue = new GitHubIssue
        {
            Number = 42,
            Title = newTitle,
            HtmlUrl = "https://github.com/myorg/myrepo/issues/42"
        };

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        _apiClient.UpdateIssueAsync(42, newTitle, null, Arg.Any<CancellationToken>())
            .Returns(issue);

        // Act
        var result = await _sut.UpdateIssueAsync(workspaceId, issueNumber, newTitle, null);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Number);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenBothTitleAndBodyAreEmpty()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        var issueNumber = "42";

        // Act
        var result = await _sut.UpdateIssueAsync(workspaceId, issueNumber, null, null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("At least one of title or body must be provided", result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenNoGitHubIntegrationExists()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration>());

        // Act
        var result = await _sut.UpdateIssueAsync(workspaceId, "42", "Title", null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No active GitHub integration", result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenAuthenticationFails()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var httpException = new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden);
        _apiClient.UpdateIssueAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.UpdateIssueAsync(workspaceId, "42", "Title", null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to authenticate", result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenIssueNotFound()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var httpException = new HttpRequestException("Not Found", null, System.Net.HttpStatusCode.NotFound);
        _apiClient.UpdateIssueAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.UpdateIssueAsync(workspaceId, "42", "Title", null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("repository not found", result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenRateLimitExceeded()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var httpException = new HttpRequestException("Too Many Requests", null, (System.Net.HttpStatusCode)429);
        _apiClient.UpdateIssueAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.UpdateIssueAsync(workspaceId, "42", "Title", null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("rate limit exceeded", result.Error);
    }
}
