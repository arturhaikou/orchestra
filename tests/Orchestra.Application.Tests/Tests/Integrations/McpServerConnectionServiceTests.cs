using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.Integrations.Services;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Exceptions;
using Orchestra.Domain.ValueObjects;

namespace Orchestra.Application.Tests.Tests.Integrations;

public class McpServerConnectionServiceTests
{
    private readonly IWorkspaceAuthorizationService _authService;
    private readonly IMcpToolDiscoveryService _discoveryService;
    private readonly McpServerConnectionService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public McpServerConnectionServiceTests()
    {
        _authService = Substitute.For<IWorkspaceAuthorizationService>();
        _discoveryService = Substitute.For<IMcpToolDiscoveryService>();

        _sut = new McpServerConnectionService(_authService, _discoveryService);
    }

    // ─── Scenario 1: Successful HTTP probe returns tool list ─────────────────

    [Fact]
    public async Task ConnectAsync_WithValidHttpRequest_ReturnsToolList()
    {
        var request = BuildHttpRequest();
        var tools = new List<DiscoveredMcpTool>
        {
            new("search-web", "Searches the web", DangerLevel.Safe, null, true),
            new("read-file", "Reads a file", DangerLevel.Moderate, null, true),
        };
        _discoveryService.DiscoverToolsAsync(
            request.Http!.Url,
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(new McpToolDiscoveryResult(tools));

        var result = await _sut.ConnectAsync(_userId, request);

        Assert.NotNull(result);
        Assert.Equal(2, result.Tools.Count);
        Assert.Contains(result.Tools, t => t.Name == "search-web");
        Assert.Contains(result.Tools, t => t.Name == "read-file");
    }

    [Fact]
    public async Task ConnectAsync_WithValidHttpRequest_MapsDescriptionToToolPreviewDto()
    {
        var request = BuildHttpRequest();
        var tools = new List<DiscoveredMcpTool>
        {
            new("search-web", "Searches the web", DangerLevel.Safe, null, true),
        };
        _discoveryService.DiscoverToolsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new McpToolDiscoveryResult(tools));

        var result = await _sut.ConnectAsync(_userId, request);

        Assert.Equal("search-web", result.Tools[0].Name);
        Assert.Equal("Searches the web", result.Tools[0].Description);
    }

    // ─── Scenario 2: Zero tools is valid success ─────────────────────────────

    [Fact]
    public async Task ConnectAsync_WhenServerReturnsNoTools_ReturnsEmptyToolList()
    {
        var request = BuildHttpRequest();
        _discoveryService.DiscoverToolsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new McpToolDiscoveryResult(new List<DiscoveredMcpTool>()));

        var result = await _sut.ConnectAsync(_userId, request);

        Assert.NotNull(result);
        Assert.Empty(result.Tools);
    }

    // ─── HTTP auth forwarding ────────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_WithApiKeyAuth_ForwardsApiKeyToDiscoveryService()
    {
        var request = BuildHttpRequest(authType: "API_KEY", apiKey: "secret-key-123");
        _discoveryService.DiscoverToolsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new McpToolDiscoveryResult(new List<DiscoveredMcpTool>()));

        await _sut.ConnectAsync(_userId, request);

        await _discoveryService.Received(1).DiscoverToolsAsync(
            request.Http!.Url,
            "API_KEY",
            "secret-key-123",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectAsync_WithNoAuth_DoesNotForwardApiKey()
    {
        var request = BuildHttpRequest(authType: "NONE", apiKey: "ignored-key");
        _discoveryService.DiscoverToolsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new McpToolDiscoveryResult(new List<DiscoveredMcpTool>()));

        await _sut.ConnectAsync(_userId, request);

        await _discoveryService.Received(1).DiscoverToolsAsync(
            Arg.Any<string>(),
            "NONE",
            Arg.Is<string?>(x => x == null),
            Arg.Any<CancellationToken>());
    }

    // ─── Stdio transport path ────────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_WithValidStdioRequest_CallsDiscoverStdioTools()
    {
        var request = BuildStdioRequest();
        _discoveryService.DiscoverStdioToolsAsync(
            Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(new McpToolDiscoveryResult(new List<DiscoveredMcpTool>()));

        await _sut.ConnectAsync(_userId, request);

        await _discoveryService.Received(1).DiscoverStdioToolsAsync(
            "npx", Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    // ─── Workspace authorization ──────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_WhenUserNotInWorkspace_ThrowsUnauthorizedWorkspaceAccessException()
    {
        var request = BuildHttpRequest();
        _authService.EnsureUserIsMemberAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(_userId, _workspaceId));

        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => _sut.ConnectAsync(_userId, request));

        await _discoveryService.DidNotReceive().DiscoverToolsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ─── Scenario 3: Connection timeout propagates ────────────────────────────

    [Fact]
    public async Task ConnectAsync_WhenMcpTimeoutThrown_PropagatesMcpConnectionException()
    {
        var request = BuildHttpRequest();
        _discoveryService.DiscoverToolsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpConnectionException(
                McpConnectionErrorCode.MCP_TIMEOUT,
                "Server did not respond within 30 seconds."));

        var ex = await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.ConnectAsync(_userId, request));

        Assert.Equal(McpConnectionErrorCode.MCP_TIMEOUT, ex.ErrorCode);
    }

    // ─── Scenario 4: Auth failure propagates ─────────────────────────────────

    [Fact]
    public async Task ConnectAsync_WhenMcpAuthFailedThrown_PropagatesMcpConnectionException()
    {
        var request = BuildHttpRequest();
        _discoveryService.DiscoverToolsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpConnectionException(
                McpConnectionErrorCode.MCP_AUTH_FAILED,
                "Authentication failed."));

        var ex = await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.ConnectAsync(_userId, request));

        Assert.Equal(McpConnectionErrorCode.MCP_AUTH_FAILED, ex.ErrorCode);
    }

    // ─── Stdio process launch failure ────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_WhenProcessLaunchFails_PropagatesProcessLaunchException()
    {
        var request = BuildStdioRequest();
        _discoveryService.DiscoverStdioToolsAsync(
            Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProcessLaunchException("npx"));

        await Assert.ThrowsAsync<ProcessLaunchException>(
            () => _sut.ConnectAsync(_userId, request));
    }

    // ─── Transport type validation ────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_WithHttpTransportButNullHttpFields_ThrowsArgumentException()
    {
        var request = new ConnectMcpServerRequest(_workspaceId, "HTTP", null, null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ConnectAsync(_userId, request));
    }

    [Fact]
    public async Task ConnectAsync_WithStdioTransportButNullStdioFields_ThrowsArgumentException()
    {
        var request = new ConnectMcpServerRequest(_workspaceId, "STDIO", null, null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ConnectAsync(_userId, request));
    }

    [Fact]
    public async Task ConnectAsync_WithUnknownTransportType_ThrowsArgumentException()
    {
        var request = new ConnectMcpServerRequest(_workspaceId, "GRPC", null, null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ConnectAsync(_userId, request));
    }

    // ─── Private builders ─────────────────────────────────────────────────────

    private ConnectMcpServerRequest BuildHttpRequest(
        string authType = "NONE",
        string? apiKey = null)
    {
        return new ConnectMcpServerRequest(
            _workspaceId,
            "HTTP",
            new ConnectHttpFields("https://mcp.example.com/api", authType, apiKey),
            null);
    }

    private ConnectMcpServerRequest BuildStdioRequest()
    {
        return new ConnectMcpServerRequest(
            _workspaceId,
            "STDIO",
            null,
            new ConnectStdioFields("npx", new[] { "-y", "my-mcp-server" }, null));
    }
}
