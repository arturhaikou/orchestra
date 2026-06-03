using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Infrastructure.Integrations.Providers.Jira;
using Orchestra.Infrastructure.Tools.Attributes;
using Orchestra.Infrastructure.Tools.Models;
using Orchestra.Infrastructure.Tools.Services;
using Orchestra.Tests.Shared.Builders;
using Orchestra.Tests.Shared.Fixtures;

namespace Orchestra.Infrastructure.Tests.Tools.Services;

public class JiraToolServiceTests : ServiceTestFixture<JiraToolService>
{
    private readonly Guid _testWorkspaceId = new("12345678-1234-1234-1234-123456789012");
    private readonly IIntegrationResolver _integrationResolver = Substitute.For<IIntegrationResolver>();
    private readonly IJiraTextContentConverter _contentConverter = Substitute.For<IJiraTextContentConverter>();
    private readonly JiraToolService _sut;

    public JiraToolServiceTests()
    {
        var mockHttpClientFactory = Substitute.For<IHttpClientFactory>();
        var mockCredentialService = Substitute.For<ICredentialEncryptionService>();
        var mockLoggerFactory = Substitute.For<ILoggerFactory>();

        var apiClientFactory = new JiraApiClientFactory(mockHttpClientFactory, mockCredentialService, mockLoggerFactory);
        var richContentBuilder = Substitute.For<IJiraRichContentBuilder>();
        _sut = new JiraToolService(apiClientFactory, _integrationResolver, Logger, _contentConverter, richContentBuilder);
    }

    #region Attribute Verification Tests

    [Fact]
    public void IJiraToolService_HasToolCategoryAttribute_WithCorrectValues()
    {
        var attr = typeof(IJiraToolService).GetCustomAttribute<ToolCategoryAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("Jira", attr!.Name);
        Assert.Equal(ProviderType.JIRA, attr.ProviderType);
        Assert.NotEmpty(attr.Description);
    }

    [Fact]
    public void IJiraToolService_HasToolActionAttribute_ForCreateIssue_WithModerateDangerLevel()
    {
        var method = typeof(IJiraToolService).GetMethod("CreateIssueAsync");
        var attr = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("create_issue", attr!.Name);
        Assert.Equal(DangerLevel.Moderate, attr.DangerLevel);
        Assert.NotEmpty(attr.Description);
    }

    [Fact]
    public void IJiraToolService_HasToolActionAttribute_ForGetIssue_WithSafeDangerLevel()
    {
        var method = typeof(IJiraToolService).GetMethod("GetIssueAsync");
        var attr = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("get_issue", attr!.Name);
        Assert.Equal(DangerLevel.Safe, attr.DangerLevel);
    }

    [Fact]
    public void IJiraToolService_HasToolActionAttribute_ForAddComment_WithModerateDangerLevel()
    {
        var method = typeof(IJiraToolService).GetMethod("AddCommentAsync");
        var attr = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("add_comment", attr!.Name);
        Assert.Equal(DangerLevel.Moderate, attr.DangerLevel);
        Assert.NotEmpty(attr.Description);
    }

    #endregion

    #region FR-02: Integration Resolution by ID Tests

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenIntegrationIdIsEmpty()
    {
        // Arrange — resolver throws when integrationId is empty (enforced by IntegrationResolver)
        _integrationResolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Is<string>(s => string.IsNullOrWhiteSpace(s)), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("integrationId is required for this tool action; no integration credentials were accessed."));

        // Act — pass empty string as integrationId
        var result = await _sut.CreateIssueAsync(
            _testWorkspaceId.ToString(),
            string.Empty,
            "Test summary",
            "Bug",
            [new ContentBlock("text", "Description")]);

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("integrationId is required", resultDict["error"].ToString());
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenIntegrationIdIsNull()
    {
        // Arrange — resolver throws when integrationId is null (enforced by IntegrationResolver)
        _integrationResolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Is<string>(s => string.IsNullOrWhiteSpace(s)), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("integrationId is required for this tool action; no integration credentials were accessed."));

        // Act
        var result = await _sut.CreateIssueAsync(
            _testWorkspaceId.ToString(),
            null!,
            "Test summary",
            "Bug",
            [new ContentBlock("text", "Description")]);

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("integrationId is required", resultDict["error"].ToString());
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenIntegrationIdNotFoundInWorkspace()
    {
        // Arrange — resolver throws when integration is not found (enforced by IntegrationResolver)
        _integrationResolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No active integration found for the supplied ID within this workspace."));

        // Act
        var result = await _sut.CreateIssueAsync(
            _testWorkspaceId.ToString(),
            Guid.NewGuid().ToString(),
            "Test summary",
            "Bug",
            [new ContentBlock("text", "Description")]);

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("No active integration found for the supplied ID", resultDict["error"].ToString());
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenIntegrationBelongsToDifferentWorkspace()
    {
        // Arrange — resolver throws not-found to prevent cross-workspace data leakage
        _integrationResolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No active integration found for the supplied ID within this workspace."));

        // Act — caller is _testWorkspaceId; integration belongs to a different workspace
        var result = await _sut.CreateIssueAsync(
            _testWorkspaceId.ToString(),
            Guid.NewGuid().ToString(),
            "Test summary",
            "Bug",
            [new ContentBlock("text", "Description")]);

        // Assert — must return not-found (no cross-workspace data leakage)
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("No active integration found for the supplied ID", resultDict["error"].ToString());
    }

    [Fact]
    public async Task CreateIssueAsync_ReturnsError_WhenIntegrationHasWrongProviderType()
    {
        // Arrange — resolver throws provider-mismatch error (enforced by IntegrationResolver)
        _integrationResolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(
                "No active Jira integration found for the supplied integrationId; the specified integration is not a Jira integration."));

        // Act
        var result = await _sut.CreateIssueAsync(
            _testWorkspaceId.ToString(),
            Guid.NewGuid().ToString(),
            "Test summary",
            "Bug",
            [new ContentBlock("text", "Description")]);

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("not a Jira integration", resultDict["error"].ToString());
    }

    #endregion

    #region AddCommentAsync Tests

    [Fact]
    public async Task AddCommentAsync_ReturnsError_WhenWorkspaceIdIsInvalid()
    {
        var result = await _sut.AddCommentAsync("not-a-guid", "integration-id", "PROJ-1", [new Orchestra.Infrastructure.Tools.Models.ContentBlock("text", "A comment", null)]);

        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Equal("INVALID_WORKSPACE_ID", resultDict["errorCode"].ToString());
    }

    [Fact]
    public async Task AddCommentAsync_ReturnsError_WhenIntegrationNotFound()
    {
        _integrationResolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Orchestra.Application.Common.Exceptions.IntegrationNotFoundException(Guid.NewGuid()));

        var result = await _sut.AddCommentAsync(
            _testWorkspaceId.ToString(), "some-integration-id", "PROJ-1", [new Orchestra.Infrastructure.Tools.Models.ContentBlock("text", "A comment", null)]);

        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Equal("INTEGRATION_NOT_FOUND", resultDict["errorCode"].ToString());
    }

    [Fact]
    public async Task AddCommentAsync_ReturnsError_WhenIntegrationIdIsEmpty()
    {
        _integrationResolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Is<string>(s => string.IsNullOrWhiteSpace(s)), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("integrationId is required for this tool action; no integration credentials were accessed."));

        var result = await _sut.AddCommentAsync(
_testWorkspaceId.ToString(), string.Empty, "PROJ-1", [new Orchestra.Infrastructure.Tools.Models.ContentBlock("text", "A comment", null)]);

        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("integrationId is required", resultDict["error"].ToString());
    }

    #endregion

    #region Image Path Validation Tests

    [Theory]
    [InlineData("./screenshots/image.png")]
    [InlineData("screenshots/image.png")]
    [InlineData("../images/screenshot.png")]
    public async Task AddCommentAsync_ReturnsInvalidArgument_WhenImagePathIsRelative(string relativePath)
    {
        // Arrange — OnPremise integration (non-atlassian.net URL) so the local-path branch is exercised
        var integration = new IntegrationBuilder()
            .WithWorkspaceId(_testWorkspaceId)
            .WithProvider(ProviderType.JIRA)
            .WithUrl("https://jira.mycompany.local")
            .Build();

        _integrationResolver
            .ResolveAsync(_testWorkspaceId, Arg.Any<string>(), ProviderType.JIRA, Arg.Any<CancellationToken>())
            .Returns(integration);

        var blocks = new List<ContentBlock>
        {
            new ContentBlock("image", relativePath, "screenshot.png")
        };

        // Act
        var result = await _sut.AddCommentAsync(
            _testWorkspaceId.ToString(), Guid.NewGuid().ToString(), "PROJ-1", blocks);

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Equal("INVALID_ARGUMENT", resultDict["errorCode"].ToString());
        Assert.Contains("relative path", resultDict["error"].ToString());
        Assert.Contains(relativePath, resultDict["error"].ToString());
    }

    [Theory]
    [InlineData("./screenshots/image.png")]
    [InlineData("screenshots/image.png")]
    public async Task CreateIssueAsync_ReturnsInvalidArgument_WhenImagePathIsRelative(string relativePath)
    {
        // Arrange — validation fires before any API call, so no integration setup needed
        var blocks = new List<ContentBlock>
        {
            new ContentBlock("text", "Description text"),
            new ContentBlock("image", relativePath, "screenshot.png")
        };

        // Act
        var result = await _sut.CreateIssueAsync(
            _testWorkspaceId.ToString(), Guid.NewGuid().ToString(), "Test issue", "Bug", blocks);

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Equal("INVALID_ARGUMENT", resultDict["errorCode"].ToString());
        Assert.Contains("relative path", resultDict["error"].ToString());
        Assert.Contains(relativePath, resultDict["error"].ToString());
    }

    #endregion

    // ── Helper ──────────────────────────────────────────────────────────────────

    private static Dictionary<string, object> SerializeToDict(object result)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
    }

    private static bool GetBooleanValue(object value)
    {
        if (value is bool b)
            return b;

        if (value is System.Text.Json.JsonElement je)
            return je.GetBoolean();

        return Convert.ToBoolean(value);
    }
}
