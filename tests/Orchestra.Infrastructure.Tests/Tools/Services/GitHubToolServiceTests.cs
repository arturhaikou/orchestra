using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Application.CodeReview;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Infrastructure.Integrations.Providers.GitHub;
using Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;
using Orchestra.Infrastructure.Tools.Attributes;
using Orchestra.Infrastructure.Tools.Services;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Infrastructure.Tests.Tools.Services;

using Orchestra.Domain.Enums;

public class GitHubToolServiceTests : ServiceTestFixture<GitHubToolService>
{
    private readonly Guid _testWorkspaceId = new("12345678-1234-1234-1234-123456789012");
    private const string TestIntegrationId = "00000000-0000-0000-0000-000000000001";
    private readonly IGitHubApiClientFactory _apiClientFactory = Substitute.For<IGitHubApiClientFactory>();
    private readonly IGitHubApiClient _apiClient = Substitute.For<IGitHubApiClient>();
    private readonly IIntegrationResolver _integrationResolver = Substitute.For<IIntegrationResolver>();
    private readonly ICodeReviewPipeline _codeReviewPipeline = Substitute.For<ICodeReviewPipeline>();
    private readonly IAiCliIntegrationDataAccess _cliIntegrationDataAccess = Substitute.For<IAiCliIntegrationDataAccess>();
    private readonly GitHubToolService _sut;

    public GitHubToolServiceTests()
    {
        _apiClientFactory.CreateClient(Arg.Any<Integration>()).Returns(_apiClient);
        _sut = new GitHubToolService(_apiClientFactory, _integrationResolver, _codeReviewPipeline, _cliIntegrationDataAccess, Logger);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsIssueData_WhenIntegrationExistsAndApiSucceeds()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

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
        var result = await _sut.GetIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "42");

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
        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

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
        var result = await _sut.GetPullRequestAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "17");

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
        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

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
        var result = await _sut.SearchIssuesAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "login bug");

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
        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);
        _apiClient.SearchIssuesAsync(Arg.Any<string>(), 30, Arg.Any<CancellationToken>())
            .Returns(new GitHubSearchResult());

        // Act
        await _sut.SearchIssuesAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "bug", limit: 100);

        // Assert — API client was called with limit clamped to 30
        await _apiClient.Received(1).SearchIssuesAsync(Arg.Any<string>(), 30, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsError_WhenNoGitHubIntegrationExists()
    {
        // Arrange
        _integrationResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No active integration found for the supplied ID within this workspace."));

        // Act
        var result = await _sut.GetIssueAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "1");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No active integration found for the supplied ID", result.Error);
    }

    [Fact]
    public async Task GetPullRequestAsync_ReturnsError_WhenNoGitHubIntegrationExists()
    {
        _integrationResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No active integration found for the supplied ID within this workspace."));

        var result = await _sut.GetPullRequestAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "1");

        Assert.False(result.Success);
        Assert.Contains("No active integration found for the supplied ID", result.Error);
    }

    [Fact]
    public async Task SearchIssuesAsync_ReturnsError_WhenNoGitHubIntegrationExists()
    {
        _integrationResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No active integration found for the supplied ID within this workspace."));

        var result = await _sut.SearchIssuesAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "bug");

        Assert.False(result.Success);
        Assert.Contains("No active integration found for the supplied ID", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsAuthError_WhenApiThrowsAuthException()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);
        _apiClient.GetIssueAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(new InvalidOperationException("Failed to authenticate with GitHub. Please verify the API key.")));

        // Act
        var result = await _sut.GetIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "1");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Failed to authenticate with GitHub. Please verify the API key.", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsNotFoundError_WhenApiThrows404Exception()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);
        _apiClient.GetIssueAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(new InvalidOperationException("GitHub repository not found or insufficient permissions.")));

        // Act
        var result = await _sut.GetIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "999");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("GitHub repository not found or insufficient permissions.", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsRateLimitError_WhenApiThrowsRateLimitException()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);
        _apiClient.GetIssueAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(new InvalidOperationException("GitHub API rate limit exceeded. Please try again later.")));

        // Act
        var result = await _sut.GetIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "1");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("GitHub API rate limit exceeded. Please try again later.", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsConnectivityError_WhenSocketExceptionThrown()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);
        var socketException = new SocketException();
        var httpException = new HttpRequestException("Network failure", socketException);
        _apiClient.GetIssueAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.GetIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "1");

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
    public void IGitHubToolService_HasToolActionAttributeForReviewPullRequest_WithModerateDangerLevel()
    {
        var method = typeof(IGitHubToolService).GetMethod("ReviewPullRequestAsync");
        var attribute = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("review_pull_request", attribute!.Name);
        Assert.Equal(DangerLevel.Moderate, attribute.DangerLevel);
        Assert.NotEmpty(attribute.Description);
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsSuccessResult_WhenIntegrationExistsAndApiSucceeds()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        var title = "Test Issue";
        var body = "This is a test issue body.";

        var issue = new GitHubIssue
        {
            Number = 42,
            Title = title,
            HtmlUrl = "https://github.com/myorg/myrepo/issues/42"
        };

        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

        _apiClient.CreateIssueAsync(title, body, null, Arg.Any<CancellationToken>())
            .Returns(issue);

        // Act
        var result = await _sut.CreateIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), title, body);

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
        _integrationResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No active integration found for the supplied ID within this workspace."));

        // Act
        var result = await _sut.CreateIssueAsync(_testWorkspaceId.ToString(), Guid.NewGuid().ToString(), "Title", "Body");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No active integration found for the supplied ID", result.Error);
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenAuthenticationFails()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

        var httpException = new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);
        _apiClient.CreateIssueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.CreateIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "Title", "Body");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to authenticate", result.Error);
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenRepositoryNotFound()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

        var httpException = new HttpRequestException("Not Found", null, System.Net.HttpStatusCode.NotFound);
        _apiClient.CreateIssueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.CreateIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "Title", "Body");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("repository not found", result.Error);
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenRateLimitExceeded()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

        var httpException = new HttpRequestException("Too Many Requests", null, (System.Net.HttpStatusCode)429);
        _apiClient.CreateIssueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.CreateIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "Title", "Body");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("rate limit exceeded", result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsSuccessResult_WhenTitleAndBodyProvided()
    {
        // Arrange
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

        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

        _apiClient.UpdateIssueAsync(42, newTitle, newBody, Arg.Any<CancellationToken>())
            .Returns(issue);

        // Act
        var result = await _sut.UpdateIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), issueNumber, newTitle, newBody);

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
        var integration = IntegrationBuilder.GitHubIntegration();
        var issueNumber = "42";
        var newTitle = "Updated Title";

        var issue = new GitHubIssue
        {
            Number = 42,
            Title = newTitle,
            HtmlUrl = "https://github.com/myorg/myrepo/issues/42"
        };

        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

        _apiClient.UpdateIssueAsync(42, newTitle, null, Arg.Any<CancellationToken>())
            .Returns(issue);

        // Act
        var result = await _sut.UpdateIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), issueNumber, newTitle, null);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Number);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenBothTitleAndBodyAreEmpty()
    {
        // Arrange
        var issueNumber = "42";

        // Act
        var result = await _sut.UpdateIssueAsync(_testWorkspaceId.ToString(), Guid.NewGuid().ToString(), issueNumber, null, null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("At least one of title or body must be provided", result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenNoGitHubIntegrationExists()
    {
        // Arrange
        _integrationResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No active integration found for the supplied ID within this workspace."));

        // Act
        var result = await _sut.UpdateIssueAsync(_testWorkspaceId.ToString(), Guid.NewGuid().ToString(), "42", "Title", null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No active integration found for the supplied ID", result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenAuthenticationFails()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

        var httpException = new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden);
        _apiClient.UpdateIssueAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.UpdateIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "42", "Title", null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to authenticate", result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenIssueNotFound()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

        var httpException = new HttpRequestException("Not Found", null, System.Net.HttpStatusCode.NotFound);
        _apiClient.UpdateIssueAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.UpdateIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "42", "Title", null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("repository not found", result.Error);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenRateLimitExceeded()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();

        _integrationResolver.ResolveAsync(integration.WorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .Returns(integration);

        var httpException = new HttpRequestException("Too Many Requests", null, (System.Net.HttpStatusCode)429);
        _apiClient.UpdateIssueAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GitHubIssue>(httpException));

        // Act
        var result = await _sut.UpdateIssueAsync(integration.WorkspaceId.ToString(), integration.Id.ToString(), "42", "Title", null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("rate limit exceeded", result.Error);
    }

    #region FR-02: Integration Resolution by ID Tests

    [Fact]
    public async Task GetIssueAsync_ReturnsError_WhenIntegrationIdIsEmpty()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        _integrationResolver
            .ResolveAsync(_testWorkspaceId, string.Empty, ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("integrationId is required"));

        // Act — pass empty string as integrationId
        var result = await _sut.GetIssueAsync(workspaceId, string.Empty, "1");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("integrationId is required", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsError_WhenIntegrationIdIsNull()
    {
        // Arrange
        var workspaceId = _testWorkspaceId.ToString();
        _integrationResolver
            .ResolveAsync(_testWorkspaceId, Arg.Any<string>(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("integrationId is required"));

        // Act — pass null as integrationId
        var result = await _sut.GetIssueAsync(workspaceId, null!, "1");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("integrationId is required", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsError_WhenIntegrationIdNotFoundInWorkspace()
    {
        // Arrange — ResolveAsync throws when integration not found
        _integrationResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No active integration found for the supplied ID within this workspace."));

        // Act
        var result = await _sut.GetIssueAsync(
            _testWorkspaceId.ToString(),
            Guid.NewGuid().ToString(),
            "1");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No active integration found for the supplied ID", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsError_WhenIntegrationBelongsToDifferentWorkspace()
    {
        // Arrange — ResolveAsync throws when workspace doesn't match
        var otherWorkspaceId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithProvider(ProviderType.GITHUB)
            .WithWorkspaceId(otherWorkspaceId)
            .Build();

        _integrationResolver.ResolveAsync(_testWorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No active integration found for the supplied ID within this workspace."));

        // Act — caller is workspace _testWorkspaceId, integration belongs to otherWorkspaceId
        var result = await _sut.GetIssueAsync(
            _testWorkspaceId.ToString(),
            integration.Id.ToString(),
            "1");

        // Assert — must return not-found (no cross-workspace data leakage)
        Assert.False(result.Success);
        Assert.Contains("No active integration found for the supplied ID", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsError_WhenIntegrationHasWrongProviderType()
    {
        // Arrange — ResolveAsync throws when provider type doesn't match
        var integration = new IntegrationBuilder()
            .WithProvider(ProviderType.GITLAB)
            .WithWorkspaceId(_testWorkspaceId)
            .Build();

        _integrationResolver.ResolveAsync(_testWorkspaceId, integration.Id.ToString(), ProviderType.GITHUB, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No active GitHub integration found for the supplied integrationId; the specified integration is not a GitHub integration."));

        // Act
        var result = await _sut.GetIssueAsync(
            _testWorkspaceId.ToString(),
            integration.Id.ToString(),
            "1");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No active GitHub integration", result.Error);
    }

    #endregion
}
