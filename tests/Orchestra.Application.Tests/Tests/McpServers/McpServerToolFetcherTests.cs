using Moq;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers;
using Orchestra.Application.McpServers.DTOs;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.McpServers;

/// <summary>
/// Unit tests for <see cref="McpServerToolFetcher"/> covering all BDD scenarios
/// from FR-004 and related edge cases.
/// TDD Phase 2 — Red: all tests fail until Phase 3 implements FetchToolsAsync.
/// </summary>
public sealed class McpServerToolFetcherTests
{
    private readonly Mock<IMcpServerDataAccess> _dataAccessMock;
    private readonly Mock<IMcpClientFactory> _clientFactoryMock;
    private readonly Mock<ICredentialEncryptionService> _encryptionMock;
    private readonly Mock<IWorkspaceAuthorizationService> _authMock;
    private readonly Mock<IMcpClient> _mcpClientMock;
    private readonly McpServerToolFetcher _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Guid _serverId = Guid.NewGuid();

    public McpServerToolFetcherTests()
    {
        _dataAccessMock = new Mock<IMcpServerDataAccess>();
        _clientFactoryMock = new Mock<IMcpClientFactory>();
        _encryptionMock = new Mock<ICredentialEncryptionService>();
        _authMock = new Mock<IWorkspaceAuthorizationService>();
        _mcpClientMock = new Mock<IMcpClient>();

        // Default: auth passes
        _authMock
            .Setup(s => s.ValidateMembershipAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default: decryption is transparent
        _encryptionMock
            .Setup(s => s.Decrypt(It.IsAny<string>()))
            .Returns((string c) => $"decrypted:{c}");

        _sut = new McpServerToolFetcher(
            _dataAccessMock.Object,
            _clientFactoryMock.Object,
            _encryptionMock.Object,
            _authMock.Object);
    }

    // ── Scenario 1: Tools loaded successfully ────────────────────────────────

    [Fact]
    public async Task FetchToolsAsync_ServerHasTools_ReturnsSuccess()
    {
        var server = new McpServerBuilder()
            .WithId(_serverId)
            .WithWorkspaceId(_workspaceId)
            .WithTransportType(McpTransportType.HTTP)
            .WithEndpointUrl("https://api.databot.com/mcp")
            .Build();

        var toolDescriptor = new Mock<IMcpToolDescriptor>();
        toolDescriptor.Setup(t => t.Name).Returns("query_data");
        toolDescriptor.Setup(t => t.Description).Returns("Queries the data store");

        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _clientFactoryMock
            .Setup(f => f.CreateClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mcpClientMock.Object);

        _mcpClientMock
            .Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { toolDescriptor.Object });

        var result = await _sut.FetchToolsAsync(_userId, _workspaceId, _serverId);

        var success = Assert.IsType<McpToolFetchResult.Success>(result);
        Assert.Single(success.Tools);
        Assert.Equal("query_data", success.Tools[0].Name);
    }

    [Fact]
    public async Task FetchToolsAsync_ServerHasDestructiveTool_ClassifiesAsDestructive()
    {
        var server = new McpServerBuilder()
            .WithId(_serverId)
            .WithWorkspaceId(_workspaceId)
            .WithTransportType(McpTransportType.HTTP)
            .WithEndpointUrl("https://api.powertools.com/mcp")
            .Build();

        var toolDescriptor = new Mock<IMcpToolDescriptor>();
        toolDescriptor.Setup(t => t.Name).Returns("delete_all_records");
        toolDescriptor.Setup(t => t.Description).Returns("Deletes all records permanently");

        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _clientFactoryMock
            .Setup(f => f.CreateClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mcpClientMock.Object);

        _mcpClientMock
            .Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { toolDescriptor.Object });

        var result = await _sut.FetchToolsAsync(_userId, _workspaceId, _serverId);

        var success = Assert.IsType<McpToolFetchResult.Success>(result);
        Assert.Equal(DangerLevel.Destructive, success.Tools[0].DangerLevel);
    }

    // ── Scenario 2: Empty tool list ──────────────────────────────────────────

    [Fact]
    public async Task FetchToolsAsync_ServerReturnsNoTools_ReturnsEmpty()
    {
        var server = new McpServerBuilder()
            .WithId(_serverId)
            .WithWorkspaceId(_workspaceId)
            .WithTransportType(McpTransportType.HTTP)
            .WithEndpointUrl("https://api.emptybot.com/mcp")
            .Build();

        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _clientFactoryMock
            .Setup(f => f.CreateClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mcpClientMock.Object);

        _mcpClientMock
            .Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<IMcpToolDescriptor>());

        var result = await _sut.FetchToolsAsync(_userId, _workspaceId, _serverId);

        Assert.IsType<McpToolFetchResult.Empty>(result);
    }

    // ── Scenario 3: Server unreachable / timeout ─────────────────────────────

    [Fact]
    public async Task FetchToolsAsync_ConnectionTimesOut_ReturnsUnreachable()
    {
        var server = new McpServerBuilder()
            .WithId(_serverId)
            .WithWorkspaceId(_workspaceId)
            .WithTransportType(McpTransportType.HTTP)
            .WithEndpointUrl("https://api.offlinebot.com/mcp")
            .Build();

        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _clientFactoryMock
            .Setup(f => f.CreateClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Connection timed out"));

        var result = await _sut.FetchToolsAsync(_userId, _workspaceId, _serverId);

        var unreachable = Assert.IsType<McpToolFetchResult.Unreachable>(result);
        Assert.NotEmpty(unreachable.Message);
    }

    [Fact]
    public async Task FetchToolsAsync_NetworkError_ReturnsUnreachable()
    {
        var server = new McpServerBuilder()
            .WithId(_serverId)
            .WithWorkspaceId(_workspaceId)
            .WithTransportType(McpTransportType.HTTP)
            .WithEndpointUrl("https://api.offlinebot.com/mcp")
            .Build();

        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _clientFactoryMock
            .Setup(f => f.CreateClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("No route to host"));

        var result = await _sut.FetchToolsAsync(_userId, _workspaceId, _serverId);

        Assert.IsType<McpToolFetchResult.Unreachable>(result);
    }

    // ── Scenario 4: Authentication failure ──────────────────────────────────

    [Fact]
    public async Task FetchToolsAsync_AuthException_ReturnsAuthFailed()
    {
        var server = new McpServerBuilder()
            .WithId(_serverId)
            .WithWorkspaceId(_workspaceId)
            .WithTransportType(McpTransportType.HTTP)
            .WithEndpointUrl("https://api.securebot.com/mcp")
            .WithAuthType(McpAuthType.API_KEY)
            .WithEncryptedApiKey("encrypted:badkey")
            .Build();

        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _clientFactoryMock
            .Setup(f => f.CreateClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mcpClientMock.Object);

        _mcpClientMock
            .Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("API key rejected"));

        var result = await _sut.FetchToolsAsync(_userId, _workspaceId, _serverId);

        Assert.IsType<McpToolFetchResult.AuthFailed>(result);
    }

    // ── Scenario 5: Cancellation (rapid switching) ───────────────────────────

    [Fact]
    public async Task FetchToolsAsync_CancelledByToken_ReturnsUnreachable()
    {
        var server = new McpServerBuilder()
            .WithId(_serverId)
            .WithWorkspaceId(_workspaceId)
            .WithTransportType(McpTransportType.HTTP)
            .WithEndpointUrl("https://api.slowbot.com/mcp")
            .Build();

        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _clientFactoryMock
            .Setup(f => f.CreateClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _sut.FetchToolsAsync(_userId, _workspaceId, _serverId, cts.Token);

        Assert.IsType<McpToolFetchResult.Unreachable>(result);
    }

    // ── Edge Cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchToolsAsync_ServerNotFound_ThrowsArgumentException()
    {
        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((McpServer?)null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.FetchToolsAsync(_userId, _workspaceId, _serverId));
    }

    [Fact]
    public async Task FetchToolsAsync_ServerBelongsToDifferentWorkspace_ThrowsWorkspaceAccessDeniedException()
    {
        var differentWorkspaceId = Guid.NewGuid();
        var server = new McpServerBuilder()
            .WithId(_serverId)
            .WithWorkspaceId(differentWorkspaceId)
            .Build();

        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        await Assert.ThrowsAsync<WorkspaceAccessDeniedException>(
            () => _sut.FetchToolsAsync(_userId, _workspaceId, _serverId));
    }

    [Fact]
    public async Task FetchToolsAsync_StdioTransport_UsesCreateStdioClient()
    {
        var server = new McpServerBuilder()
            .WithId(_serverId)
            .WithWorkspaceId(_workspaceId)
            .WithTransportType(McpTransportType.STDIO)
            .WithCommand("npx -y @my/mcp-server")
            .Build();

        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _clientFactoryMock
            .Setup(f => f.CreateStdioClientAsync(
                It.IsAny<string>(), It.IsAny<string[]?>(),
                It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mcpClientMock.Object);

        _mcpClientMock
            .Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<IMcpToolDescriptor>());

        await _sut.FetchToolsAsync(_userId, _workspaceId, _serverId);

        _clientFactoryMock.Verify(
            f => f.CreateStdioClientAsync(
                It.IsAny<string>(), It.IsAny<string[]?>(),
                It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _clientFactoryMock.Verify(
            f => f.CreateClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchToolsAsync_ToolWithNullDescription_DoesNotThrow()
    {
        var server = new McpServerBuilder()
            .WithId(_serverId)
            .WithWorkspaceId(_workspaceId)
            .WithTransportType(McpTransportType.HTTP)
            .WithEndpointUrl("https://api.databot.com/mcp")
            .Build();

        var toolDescriptor = new Mock<IMcpToolDescriptor>();
        toolDescriptor.Setup(t => t.Name).Returns("get_data");
        toolDescriptor.Setup(t => t.Description).Returns((string?)null);

        _dataAccessMock
            .Setup(d => d.GetByIdAsync(_serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _clientFactoryMock
            .Setup(f => f.CreateClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mcpClientMock.Object);

        _mcpClientMock
            .Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { toolDescriptor.Object });

        var result = await _sut.FetchToolsAsync(_userId, _workspaceId, _serverId);

        Assert.IsType<McpToolFetchResult.Success>(result);
    }
}
