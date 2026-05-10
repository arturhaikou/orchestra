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
        _sut = new JiraToolService(apiClientFactory, _integrationResolver, Logger, _contentConverter);
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
            "Description",
            "Bug");

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
            "Description",
            "Bug");

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
            "Description",
            "Bug");

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
            "Description",
            "Bug");

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
            "Description",
            "Bug");

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("not a Jira integration", resultDict["error"].ToString());
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
