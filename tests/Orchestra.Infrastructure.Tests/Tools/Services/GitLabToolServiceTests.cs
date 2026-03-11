using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;
using Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;
using Orchestra.Infrastructure.Tools.Attributes;
using Orchestra.Infrastructure.Tools.Services;
using Orchestra.Tests.Shared.Builders;
using Orchestra.Tests.Shared;

namespace Orchestra.Infrastructure.Tests.Tools.Services;

public class GitLabToolServiceTests : ServiceTestFixture<GitLabToolService>
{
    private readonly Guid _testWorkspaceId = new("12345678-1234-1234-1234-123456789012");
    private readonly IGitLabApiClientFactory _apiClientFactory = Substitute.For<IGitLabApiClientFactory>();
    private readonly IGitLabApiClient _apiClient = Substitute.For<IGitLabApiClient>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly GitLabToolService _sut;

    public GitLabToolServiceTests()
    {
        _apiClientFactory.CreateClient(Arg.Any<Integration>()).Returns(_apiClient);
        _sut = new GitLabToolService(_apiClientFactory, _integrationDataAccess, Logger);
    }

    #region Attribute Verification Tests

    [Fact]
    public void IGitLabToolService_HasToolCategoryAttribute_WithCorrectValues()
    {
        var attr = typeof(IGitLabToolService).GetCustomAttribute<ToolCategoryAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("GitLab", attr!.Name);
        Assert.Equal(ProviderType.GITLAB, attr.ProviderType);
        Assert.NotEmpty(attr.Description);
    }

    [Fact]
    public void IGitLabToolService_HasToolActionAttribute_ForGetIssue_WithSafeDangerLevel()
    {
        var method = typeof(IGitLabToolService).GetMethod("GetIssueAsync");
        var attr = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("get_issue", attr!.Name);
        Assert.Equal(DangerLevel.Safe, attr.DangerLevel);
        Assert.NotEmpty(attr.Description);
    }

    [Fact]
    public void IGitLabToolService_HasToolActionAttribute_ForGetMr_WithSafeDangerLevel()
    {
        var method = typeof(IGitLabToolService).GetMethod("GetMergeRequestAsync");
        var attr = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("get_mr", attr!.Name);
        Assert.Equal(DangerLevel.Safe, attr.DangerLevel);
        Assert.NotEmpty(attr.Description);
    }

    [Fact]
    public void IGitLabToolService_HasToolActionAttribute_ForSearchIssues_WithSafeDangerLevel()
    {
        var method = typeof(IGitLabToolService).GetMethod("SearchIssuesAsync");
        var attr = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("search_issues", attr!.Name);
        Assert.Equal(DangerLevel.Safe, attr.DangerLevel);
        Assert.NotEmpty(attr.Description);
    }

    [Fact]
    public void IGitLabToolService_HasToolActionAttribute_ForCreateIssue_WithModerateDangerLevel()
    {
        var method = typeof(IGitLabToolService).GetMethod("CreateIssueAsync");
        var attr = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("create_issue", attr!.Name);
        Assert.Equal(DangerLevel.Moderate, attr.DangerLevel);
        Assert.NotEmpty(attr.Description);
    }

    [Fact]
    public void IGitLabToolService_HasToolActionAttribute_ForUpdateIssue_WithModerateDangerLevel()
    {
        var method = typeof(IGitLabToolService).GetMethod("UpdateIssueAsync");
        var attr = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("update_issue", attr!.Name);
        Assert.Equal(DangerLevel.Moderate, attr.DangerLevel);
        Assert.NotEmpty(attr.Description);
    }

    #endregion

    #region GetIssueAsync Tests

    [Fact]
    public async Task GetIssueAsync_ReturnsIssueData_WhenIntegrationExistsAndApiSucceeds()
    {
        var integration = IntegrationBuilder.GitLabIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var issue = new GitLabIssue
        {
            Iid = 42,
            Title = "Fix critical bug",
            Description = "Details about the bug",
            State = "opened",
            WebUrl = "https://gitlab.com/myorg/myproject/-/issues/42"
        };
        _apiClient.GetIssueAsync(42, Arg.Any<CancellationToken>()).Returns(issue);

        var result = await _sut.GetIssueAsync(workspaceId, "42");

        Assert.True(result.Success);
        Assert.Equal(42, result.Iid);
        Assert.Equal("Fix critical bug", result.Title);
        Assert.Equal("Details about the bug", result.Description);
        Assert.Equal("opened", result.State);
        Assert.Equal("https://gitlab.com/myorg/myproject/-/issues/42", result.Url);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsError_WhenNoGitLabIntegrationExists()
    {
        var workspaceId = _testWorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration>());

        var result = await _sut.GetIssueAsync(workspaceId, "42");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
        Assert.Contains("No active GitLab integration", result.Error);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsError_WhenIssueNotFound()
    {
        var integration = IntegrationBuilder.GitLabIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        _apiClient.GetIssueAsync(999, Arg.Any<CancellationToken>()).Returns((GitLabIssue?)null);

        var result = await _sut.GetIssueAsync(workspaceId, "999");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetIssueAsync_ReturnsError_WhenInvalidWorkspaceId()
    {
        var result = await _sut.GetIssueAsync("not-a-guid", "42");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
        Assert.Contains("Invalid workspace ID", result.Error);
    }

    #endregion

    #region GetMergeRequestAsync Tests

    [Fact]
    public async Task GetMergeRequestAsync_ReturnsMergeRequestData_WhenIntegrationExistsAndApiSucceeds()
    {
        var integration = IntegrationBuilder.GitLabIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var mergeRequest = new GitLabMergeRequest
        {
            Iid = 17,
            Title = "Add feature",
            Description = "Feature description",
            State = "opened",
            MergedAt = null,
            WebUrl = "https://gitlab.com/myorg/myproject/-/merge_requests/17",
            SourceBranch = "feature/my-branch",
            TargetBranch = "main"
        };
        _apiClient.GetMergeRequestAsync(17, Arg.Any<CancellationToken>()).Returns(mergeRequest);

        var result = await _sut.GetMergeRequestAsync(workspaceId, "17");

        Assert.True(result.Success);
        Assert.Equal(17, result.Iid);
        Assert.Equal("Add feature", result.Title);
        Assert.False(result.Merged);
        Assert.Equal("feature/my-branch", result.SourceBranch);
        Assert.Equal("main", result.TargetBranch);
    }

    [Fact]
    public async Task GetMergeRequestAsync_ReturnsError_WhenNoGitLabIntegrationExists()
    {
        var workspaceId = _testWorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(_testWorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration>());

        var result = await _sut.GetMergeRequestAsync(workspaceId, "17");

        Assert.False(result.Success);
        Assert.Contains("No active GitLab integration", result.Error!);
    }

    #endregion

    #region SearchIssuesAsync Tests

    [Fact]
    public async Task SearchIssuesAsync_ReturnsSearchResults_WhenIntegrationExistsAndApiSucceeds()
    {
        var integration = IntegrationBuilder.GitLabIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var issues = new List<GitLabIssue>
        {
            new() { Iid = 1, Title = "Issue A", State = "opened", WebUrl = "https://..." },
            new() { Iid = 2, Title = "Issue B", State = "closed", WebUrl = "https://..." }
        };
        _apiClient.SearchIssuesAsync("login bug", 10, Arg.Any<CancellationToken>()).Returns(issues);

        var result = await _sut.SearchIssuesAsync(workspaceId, "login bug");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.NotNull(result.Items);
        Assert.Equal(2, result.Items.Length);
    }

    [Fact]
    public async Task SearchIssuesAsync_ClampsLimitTo30_WhenLimitExceedsMax()
    {
        var integration = IntegrationBuilder.GitLabIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });
        _apiClient.SearchIssuesAsync(Arg.Any<string>(), 30, Arg.Any<CancellationToken>())
            .Returns(new List<GitLabIssue>());

        await _sut.SearchIssuesAsync(workspaceId, "bug", limit: 100);

        await _apiClient.Received(1).SearchIssuesAsync(Arg.Any<string>(), 30, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchIssuesAsync_ReturnsError_WhenQueryIsEmpty()
    {
        var integration = IntegrationBuilder.GitLabIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var result = await _sut.SearchIssuesAsync(workspaceId, "   ");

        Assert.False(result.Success);
        Assert.Contains("empty", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region CreateIssueAsync Tests

    [Fact]
    public async Task CreateIssueAsync_ReturnsCreateResult_WhenIntegrationExistsAndApiSucceeds()
    {
        var integration = IntegrationBuilder.GitLabIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var createdIssue = new GitLabIssue
        {
            Iid = 100,
            Title = "New issue",
            Description = "Issue description",
            State = "opened",
            WebUrl = "https://gitlab.com/myorg/myproject/-/issues/100"
        };
        _apiClient.CreateIssueAsync("New issue", "Issue description", Arg.Any<List<string>?>(), Arg.Any<CancellationToken>())
            .Returns(createdIssue);

        var result = await _sut.CreateIssueAsync(workspaceId, "New issue", "Issue description");

        Assert.True(result.Success);
        Assert.Equal(100, result.Iid);
        Assert.Equal("New issue", result.Title);
        Assert.Equal("https://gitlab.com/myorg/myproject/-/issues/100", result.Url);
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenTitleIsEmpty()
    {
        var integration = IntegrationBuilder.GitLabIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var result = await _sut.CreateIssueAsync(workspaceId, "   ", "description");

        Assert.False(result.Success);
        Assert.Contains("empty", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region UpdateIssueAsync Tests

    [Fact]
    public async Task UpdateIssueAsync_ReturnsUpdateResult_WhenIntegrationExistsAndApiSucceeds()
    {
        var integration = IntegrationBuilder.GitLabIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var updatedIssue = new GitLabIssue
        {
            Iid = 42,
            Title = "Updated title",
            Description = "Updated description",
            State = "opened",
            WebUrl = "https://gitlab.com/myorg/myproject/-/issues/42"
        };
        _apiClient.UpdateIssueAsync(42, "Updated title", "Updated description", Arg.Any<CancellationToken>())
            .Returns(updatedIssue);

        var result = await _sut.UpdateIssueAsync(workspaceId, "42", "Updated title", "Updated description");

        Assert.True(result.Success);
        Assert.Equal(42, result.Iid);
        Assert.Equal("Updated title", result.Title);
    }

    [Fact]
    public async Task UpdateIssueAsync_ReturnsError_WhenTitleAndDescriptionAreNull()
    {
        var integration = IntegrationBuilder.GitLabIntegration();
        var workspaceId = integration.WorkspaceId.ToString();
        _integrationDataAccess.GetByWorkspaceIdAsync(integration.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        var result = await _sut.UpdateIssueAsync(workspaceId, "42", null, null);

        Assert.False(result.Success);
        Assert.Contains("one of", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
