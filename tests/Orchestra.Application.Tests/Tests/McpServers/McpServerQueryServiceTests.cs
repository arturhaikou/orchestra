using Moq;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.McpServers;

/// <summary>
/// Unit tests for <see cref="McpServerQueryService"/> covering FR-001 Scenario 3 (list servers),
/// Scenario 5 cross-workspace denial (query operations), GetByIdAsync happy path, and edge cases.
/// TDD Phase 2 — Red: all tests are expected to fail until implementation is complete.
/// </summary>
public sealed class McpServerQueryServiceTests
{
    private readonly Mock<IWorkspaceAuthorizationService> _authMock;
    private readonly Mock<IMcpServerDataAccess> _dataAccessMock;
    private readonly Mock<IMcpServerConnectionChecker> _connectionCheckerMock;
    private readonly McpServerQueryService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public McpServerQueryServiceTests()
    {
        _authMock = new Mock<IWorkspaceAuthorizationService>();
        _dataAccessMock = new Mock<IMcpServerDataAccess>();
        _connectionCheckerMock = new Mock<IMcpServerConnectionChecker>();

        _authMock
            .Setup(s => s.ValidateMembershipAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _connectionCheckerMock
            .Setup(c => c.CheckAsync(It.IsAny<McpServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpConnectionStatus.Connected);

        _sut = new McpServerQueryService(
            _authMock.Object,
            _dataAccessMock.Object,
            _connectionCheckerMock.Object);
    }

    // ── Scenario 3: Listing MCP servers for a workspace ──────────────────────

    [Fact]
    public async Task GetListAsync_WithWorkspaceHaving3Servers_ReturnsAll3()
    {
        var servers = Enumerable.Range(1, 3)
            .Select(i => new McpServerBuilder()
                .WithWorkspaceId(_workspaceId)
                .WithName($"Server {i}")
                .Build())
            .ToList();

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(servers);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetListAsync_WithWorkspaceHaving3Servers_ReturnsDtosWithCorrectFields()
    {
        var server = new McpServerBuilder()
            .WithWorkspaceId(_workspaceId)
            .WithName("Analytics Tools")
            .WithTransportType(McpTransportType.HTTP)
            .Build();

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([server]);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Single(result);
        Assert.Equal("Analytics Tools", result[0].Name);
        Assert.Equal("HTTP", result[0].TransportType);
        Assert.Equal("Connected", result[0].ConnectionStatus);
    }

    [Fact]
    public async Task GetListAsync_WithNoServers_ReturnsEmptyList()
    {
        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Empty(result);
    }

    // ── Scenario 5: Cross-workspace access denied (query) ────────────────────

    [Fact]
    public async Task GetListAsync_WhenUserNotMemberOfWorkspace_ThrowsWorkspaceAccessDeniedException()
    {
        _authMock
            .Setup(s => s.ValidateMembershipAsync(_userId, _workspaceId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkspaceAccessDeniedException(_userId, _workspaceId));

        await Assert.ThrowsAsync<WorkspaceAccessDeniedException>(
            () => _sut.GetListAsync(_userId, _workspaceId));
    }

    [Fact]
    public async Task GetListAsync_WhenAccessDenied_DoesNotCallDataAccess()
    {
        _authMock
            .Setup(s => s.ValidateMembershipAsync(_userId, _workspaceId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkspaceAccessDeniedException(_userId, _workspaceId));

        await Assert.ThrowsAsync<WorkspaceAccessDeniedException>(
            () => _sut.GetListAsync(_userId, _workspaceId));

        _dataAccessMock.Verify(
            s => s.GetByWorkspaceIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserNotMemberOfWorkspace_ThrowsWorkspaceAccessDeniedException()
    {
        _authMock
            .Setup(s => s.ValidateMembershipAsync(_userId, _workspaceId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkspaceAccessDeniedException(_userId, _workspaceId));

        await Assert.ThrowsAsync<WorkspaceAccessDeniedException>(
            () => _sut.GetByIdAsync(_userId, _workspaceId, Guid.NewGuid()));
    }

    // ── Edge: GetById happy path ──────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenServerExists_ReturnsFullDto()
    {
        var serverId = Guid.NewGuid();
        var server = new McpServerBuilder()
            .WithWorkspaceId(_workspaceId)
            .WithName("Detailed Server")
            .WithEndpointUrl("https://api.example.com/mcp")
            .WithAuthType(McpAuthType.API_KEY)
            .WithEncryptedApiKey("encrypted:secret")
            .Build();

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        var result = await _sut.GetByIdAsync(_userId, _workspaceId, serverId);

        Assert.NotNull(result);
        Assert.Equal("Detailed Server", result.Name);
        Assert.Equal("HTTP", result.TransportType);
        Assert.True(result.HasApiKey);
        Assert.Null(result.AuthType);
    }

    [Fact]
    public async Task GetByIdAsync_WhenServerNotFound_ThrowsArgumentException()
    {
        _dataAccessMock
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Orchestra.Domain.Entities.McpServer?)null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetByIdAsync(_userId, _workspaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdAsync_WhenServerBelongsToOtherWorkspace_ThrowsWorkspaceAccessDeniedException()
    {
        var serverId = Guid.NewGuid();
        var otherWorkspace = Guid.NewGuid();
        var server = new McpServerBuilder()
            .WithWorkspaceId(otherWorkspace)
            .Build();

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        await Assert.ThrowsAsync<WorkspaceAccessDeniedException>(
            () => _sut.GetByIdAsync(_userId, _workspaceId, serverId));
    }

    // ── Scenario 2: Live connection check ─────────────────────────────────────

    [Fact]
    public async Task GetListAsync_WithTwoServers_CallsCheckAsyncOnEachServer()
    {
        var servers = Enumerable.Range(1, 2)
            .Select(_ => new McpServerBuilder().WithWorkspaceId(_workspaceId).Build())
            .ToList();

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(servers);

        await _sut.GetListAsync(_userId, _workspaceId);

        _connectionCheckerMock.Verify(
            c => c.CheckAsync(It.IsAny<McpServer>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetListAsync_WhenCheckerReturnsConnected_DtoShowsConnectedStatus()
    {
        var server = new McpServerBuilder().WithWorkspaceId(_workspaceId).Build();

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([server]);
        _connectionCheckerMock
            .Setup(c => c.CheckAsync(server, It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpConnectionStatus.Connected);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Equal("Connected", result[0].ConnectionStatus);
    }

    [Fact]
    public async Task GetListAsync_WhenCheckerReturnsConnectionFailed_DtoShowsConnectionFailedStatus()
    {
        var server = new McpServerBuilder().WithWorkspaceId(_workspaceId).Build();

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([server]);
        _connectionCheckerMock
            .Setup(c => c.CheckAsync(server, It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpConnectionStatus.ConnectionFailed);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Equal("ConnectionFailed", result[0].ConnectionStatus);
    }

    // ── Scenario 3: Failed server still visible ───────────────────────────────

    [Fact]
    public async Task GetListAsync_WhenCheckerReturnsConnectionFailed_ServerIsStillIncludedInResult()
    {
        var server = new McpServerBuilder()
            .WithWorkspaceId(_workspaceId)
            .WithName("Unreachable Server")
            .Build();

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([server]);
        _connectionCheckerMock
            .Setup(c => c.CheckAsync(server, It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpConnectionStatus.ConnectionFailed);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Single(result);
        Assert.Equal("Unreachable Server", result[0].Name);
        Assert.Equal("ConnectionFailed", result[0].ConnectionStatus);
    }

    // ── Scenario 4: No servers ────────────────────────────────────────────────

    [Fact]
    public async Task GetListAsync_WithNoServers_DoesNotCallConnectionChecker()
    {
        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Empty(result);
        _connectionCheckerMock.Verify(
            c => c.CheckAsync(It.IsAny<McpServer>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Edge case: Live check overrides stored status ─────────────────────────

    [Fact]
    public async Task GetListAsync_WhenServerWentOffline_UsesLiveStatusNotStoredStatus()
    {
        var server = new McpServerBuilder().WithWorkspaceId(_workspaceId).Build();

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([server]);
        _connectionCheckerMock
            .Setup(c => c.CheckAsync(server, It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpConnectionStatus.ConnectionFailed);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Equal("ConnectionFailed", result[0].ConnectionStatus);
    }

    [Fact]
    public async Task GetListAsync_WithThreeServers_ReturnsPerServerLiveStatus()
    {
        var servers = Enumerable.Range(1, 3)
            .Select(i => new McpServerBuilder()
                .WithWorkspaceId(_workspaceId)
                .WithName($"Server{i}")
                .Build())
            .ToList();

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(servers);

        _connectionCheckerMock
            .Setup(c => c.CheckAsync(servers[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpConnectionStatus.Connected);
        _connectionCheckerMock
            .Setup(c => c.CheckAsync(servers[1], It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpConnectionStatus.ConnectionFailed);
        _connectionCheckerMock
            .Setup(c => c.CheckAsync(servers[2], It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpConnectionStatus.Connected);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Equal(3, result.Count);
        var s1 = result.First(r => r.Name == "Server1");
        var s2 = result.First(r => r.Name == "Server2");
        var s3 = result.First(r => r.Name == "Server3");
        Assert.Equal("Connected", s1.ConnectionStatus);
        Assert.Equal("ConnectionFailed", s2.ConnectionStatus);
        Assert.Equal("Connected", s3.ConnectionStatus);
    }

    // ── Auth always checked before data access ────────────────────────────────

    [Fact]
    public async Task GetListAsync_AuthValidatedBeforeDataAccess()
    {
        var callOrder = new List<string>();

        _authMock
            .Setup(s => s.ValidateMembershipAsync(_userId, _workspaceId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("auth"))
            .Returns(Task.CompletedTask);

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("dataAccess"))
            .ReturnsAsync([]);

        await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Equal(["auth", "dataAccess"], callOrder);
    }
}
