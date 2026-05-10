using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Infrastructure.Integrations.Providers.Confluence;
using Orchestra.Infrastructure.Tools.Attributes;
using Orchestra.Infrastructure.Tools.Services;
using Orchestra.Tests.Shared.Builders;
using Orchestra.Tests.Shared.Fixtures;

namespace Orchestra.Infrastructure.Tests.Tools.Services;

public class ConfluenceToolServiceTests : ServiceTestFixture<ConfluenceToolService>
{
    private readonly Guid _testWorkspaceId = new("12345678-1234-1234-1234-123456789012");
    private readonly IIntegrationResolver _integrationResolver = Substitute.For<IIntegrationResolver>();
    private readonly IAdfConversionService _adfConversionService = Substitute.For<IAdfConversionService>();
    private readonly ConfluenceToolService _sut;

    public ConfluenceToolServiceTests()
    {
        var mockHttpClientFactory = Substitute.For<IHttpClientFactory>();
        var mockCredentialService = Substitute.For<ICredentialEncryptionService>();
        var mockLoggerFactory = Substitute.For<ILoggerFactory>();

        var apiClientFactory = new ConfluenceApiClientFactory(mockHttpClientFactory, mockCredentialService, mockLoggerFactory);
        _sut = new ConfluenceToolService(apiClientFactory, _integrationResolver, _adfConversionService, Logger);
    }

    #region Attribute Verification Tests

    [Fact]
    public void IConfluenceToolService_HasToolCategoryAttribute_WithCorrectValues()
    {
        var attr = typeof(IConfluenceToolService).GetCustomAttribute<ToolCategoryAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("Confluence", attr!.Name);
        Assert.Equal(ProviderType.CONFLUENCE, attr.ProviderType);
        Assert.NotEmpty(attr.Description);
    }

    [Fact]
    public void IConfluenceToolService_HasToolActionAttribute_ForSearch_WithSafeDangerLevel()
    {
        var method = typeof(IConfluenceToolService).GetMethod("SearchAsync");
        var attr = method?.GetCustomAttribute<ToolActionAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("search", attr!.Name);
        Assert.Equal(DangerLevel.Safe, attr.DangerLevel);
    }

    #endregion

    #region FR-02: Integration Resolution by ID Tests

    [Fact]
    public async Task SearchAsync_ReturnsError_WhenIntegrationIdIsEmpty()
    {
        // Act
        var result = await _sut.SearchAsync(
            _testWorkspaceId.ToString(),
            string.Empty,
            "search query");

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("integrationId is required", resultDict["error"].ToString());

        await _integrationResolver.DidNotReceive()
            .ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>());
    }

    [Fact]
    public async Task SearchAsync_ReturnsError_WhenIntegrationIdIsNull()
    {
        // Act
        var result = await _sut.SearchAsync(
            _testWorkspaceId.ToString(),
            null!,
            "search query");

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("integrationId is required", resultDict["error"].ToString());
    }

    [Fact]
    public async Task SearchAsync_ReturnsError_WhenIntegrationIdNotFoundInWorkspace()
    {
        // Arrange
        var integrationId = Guid.NewGuid().ToString();
        var ex = new IntegrationNotFoundException(Guid.NewGuid());
        _integrationResolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromException<Integration>(ex));

        // Act
        var result = await _sut.SearchAsync(
            _testWorkspaceId.ToString(),
            integrationId,
            "search query");

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Equal("CONFLUENCE_INTEGRATION_NOT_FOUND", resultDict["errorCode"].ToString());
    }

    [Fact]
    public async Task SearchAsync_ReturnsError_WhenIntegrationBelongsToDifferentWorkspace()
    {
        // Arrange
        var otherWorkspaceId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithProvider(ProviderType.CONFLUENCE)
            .WithWorkspaceId(otherWorkspaceId)
            .Build();

        // ResolveAsync throws InvalidOperationException (cross-workspace access prevented)
        var ex = new InvalidOperationException("No active integration found for the supplied ID within this workspace.");
        _integrationResolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromException<Integration>(ex));

        // Act — caller is _testWorkspaceId; integration belongs to otherWorkspaceId
        var result = await _sut.SearchAsync(
            _testWorkspaceId.ToString(),
            integration.Id.ToString(),
            "search query");

        // Assert — must return not-found (no cross-workspace data leakage)
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("No active integration found for the supplied ID", resultDict["error"].ToString());
    }

    [Fact]
    public async Task SearchAsync_ReturnsError_WhenIntegrationHasWrongProviderType()
    {
        // Arrange — Jira integration for the correct workspace but wrong provider
        var integration = new IntegrationBuilder()
            .WithProvider(ProviderType.JIRA)
            .WithWorkspaceId(_testWorkspaceId)
            .Build();

        // ResolveAsync throws InvalidOperationException (wrong provider type)
        var ex = new InvalidOperationException("No active Confluence integration found for the supplied integrationId; the specified integration is not a Confluence integration.");
        _integrationResolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ProviderType>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromException<Integration>(ex));

        // Act
        var result = await _sut.SearchAsync(
            _testWorkspaceId.ToString(),
            integration.Id.ToString(),
            "search query");

        // Assert
        var resultDict = SerializeToDict(result);
        Assert.False(GetBooleanValue(resultDict["success"]));
        Assert.Contains("not a Confluence integration", resultDict["error"].ToString());
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
