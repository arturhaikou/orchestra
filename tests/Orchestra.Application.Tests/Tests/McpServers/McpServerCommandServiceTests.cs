using Moq;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.McpServers;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Application.Tests.Tests.McpServers;

/// <summary>
/// Unit tests for <see cref="McpServerCommandService"/> covering all state-changing
/// BDD scenarios from FR-001 (Scenarios 1, 2, 4, 5) and related edge cases.
/// TDD Phase 2 — Red: all tests are expected to fail until implementation is complete.
/// </summary>
public sealed class McpServerCommandServiceTests
{
    private readonly Mock<IWorkspaceAuthorizationService> _authMock;
    private readonly Mock<IMcpServerDataAccess> _dataAccessMock;
    private readonly Mock<ICredentialEncryptionService> _encryptionMock;
    private readonly Mock<IMcpServerImpactCounter> _impactCounterMock;
    private readonly McpServerCommandService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public McpServerCommandServiceTests()
    {
        _authMock = new Mock<IWorkspaceAuthorizationService>();
        _dataAccessMock = new Mock<IMcpServerDataAccess>();
        _encryptionMock = new Mock<ICredentialEncryptionService>();
        _impactCounterMock = new Mock<IMcpServerImpactCounter>();

        // Default: auth passes
        _authMock
            .Setup(s => s.ValidateMembershipAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default: name is unique
        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Default: encrypt returns a placeholder
        _encryptionMock
            .Setup(s => s.Encrypt(It.IsAny<string>()))
            .Returns((string p) => $"encrypted:{p}");

        // Default: zero impacted agents
        _impactCounterMock
            .Setup(s => s.CountImpactedAgentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _sut = new McpServerCommandService(
            _authMock.Object,
            _dataAccessMock.Object,
            _encryptionMock.Object,
            _impactCounterMock.Object);
    }

    // ── Scenario 1: Successfully creating an HTTP MCP server ─────────────────

    [Fact]
    public async Task CreateAsync_WithValidHttpRequest_CreatesServerAndReturnsDto()
    {
        var request = new SaveMcpServerRequest(
            WorkspaceId: _workspaceId,
            Name: "My HTTP Server",
            TransportType: "HTTP",
            Http: new SaveHttpFields("https://api.example.com/mcp", "NONE", null),
            Stdio: null);

        var result = await _sut.CreateAsync(_userId, request);

        Assert.NotNull(result);
        Assert.Equal("My HTTP Server", result.Name);
        Assert.Equal("HTTP", result.TransportType);
        Assert.Equal("Unknown", result.ConnectionStatus);
        _dataAccessMock.Verify(
            s => s.AddAsync(It.IsAny<McpServer>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithValidStdioRequest_CreatesServerAndReturnsDto()
    {
        var request = new SaveMcpServerRequest(
            WorkspaceId: _workspaceId,
            Name: "My STDIO Server",
            TransportType: "STDIO",
            Http: null,
            Stdio: new SaveStdioFields("npx", ["-y", "@scope/server"], null));

        var result = await _sut.CreateAsync(_userId, request);

        Assert.NotNull(result);
        Assert.Equal("My STDIO Server", result.Name);
        Assert.Equal("STDIO", result.TransportType);
        _dataAccessMock.Verify(
            s => s.AddAsync(It.IsAny<McpServer>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithApiKey_EncryptsKeyBeforeStorage()
    {
        var request = new SaveMcpServerRequest(
            WorkspaceId: _workspaceId,
            Name: "Secured Server",
            TransportType: "HTTP",
            Http: new SaveHttpFields("https://api.example.com/mcp", "API_KEY", "my-secret-key"),
            Stdio: null);

        await _sut.CreateAsync(_userId, request);

        _encryptionMock.Verify(s => s.Encrypt("my-secret-key"), Times.Once);
    }

    // ── Scenario 2: Duplicate name rejected ──────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenNameAlreadyExistsInWorkspace_ThrowsValidationException()
    {
        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(_workspaceId, "My Tools", null, default))
            .ReturnsAsync(true);

        var request = new SaveMcpServerRequest(
            WorkspaceId: _workspaceId,
            Name: "My Tools",
            TransportType: "HTTP",
            Http: new SaveHttpFields("https://api.example.com/mcp", "NONE", null),
            Stdio: null);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.CreateAsync(_userId, request));
    }

    [Fact]
    public async Task CreateAsync_WhenNameIsDuplicate_DoesNotCallAddAsync()
    {
        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(_workspaceId, "Duplicate", null, default))
            .ReturnsAsync(true);

        var request = new SaveMcpServerRequest(
            WorkspaceId: _workspaceId,
            Name: "Duplicate",
            TransportType: "HTTP",
            Http: new SaveHttpFields("https://api.example.com/mcp", "NONE", null),
            Stdio: null);

        await Assert.ThrowsAsync<ValidationException>(() => _sut.CreateAsync(_userId, request));

        _dataAccessMock.Verify(
            s => s.AddAsync(It.IsAny<McpServer>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Scenario 4: Delete with active agent assignments ─────────────────────

    [Fact]
    public async Task DeleteAsync_WhenAgentsUseServer_ReturnsCorrectAffectedCount()
    {
        var serverId = Guid.NewGuid();
        var server = new McpServerBuilder()
            .WithWorkspaceId(_workspaceId)
            .Build();

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _impactCounterMock
            .Setup(s => s.CountImpactedAgentsAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _sut.DeleteAsync(_userId, serverId, _workspaceId);

        Assert.Equal(2, result.AffectedAgentCount);
    }

    [Fact]
    public async Task DeleteAsync_Always_CallsDataAccessDeleteOnTheCorrectServer()
    {
        var serverId = Guid.NewGuid();
        McpServer? capturedServer = null;
        var server = new McpServerBuilder()
            .WithWorkspaceId(_workspaceId)
            .Build();

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _dataAccessMock
            .Setup(s => s.DeleteAsync(It.IsAny<McpServer>(), It.IsAny<CancellationToken>()))
            .Callback<McpServer, CancellationToken>((s, _) => capturedServer = s)
            .Returns(Task.CompletedTask);

        await _sut.DeleteAsync(_userId, serverId, _workspaceId);

        _dataAccessMock.Verify(
            s => s.DeleteAsync(It.IsAny<McpServer>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Same(server, capturedServer);
    }

    // ── Scenario 5: Cross-workspace access denied ────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenUserNotMemberOfWorkspace_ThrowsWorkspaceAccessDeniedException()
    {
        _authMock
            .Setup(s => s.ValidateMembershipAsync(_userId, _workspaceId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkspaceAccessDeniedException(_userId, _workspaceId));

        var request = new SaveMcpServerRequest(
            WorkspaceId: _workspaceId,
            Name: "Any Server",
            TransportType: "HTTP",
            Http: new SaveHttpFields("https://api.example.com/mcp", "NONE", null),
            Stdio: null);

        await Assert.ThrowsAsync<WorkspaceAccessDeniedException>(
            () => _sut.CreateAsync(_userId, request));
    }

    [Fact]
    public async Task DeleteAsync_WhenUserNotMemberOfWorkspace_ThrowsWorkspaceAccessDeniedException()
    {
        _authMock
            .Setup(s => s.ValidateMembershipAsync(_userId, _workspaceId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkspaceAccessDeniedException(_userId, _workspaceId));

        await Assert.ThrowsAsync<WorkspaceAccessDeniedException>(
            () => _sut.DeleteAsync(_userId, Guid.NewGuid(), _workspaceId));
    }

    [Fact]
    public async Task DeleteAsync_WhenServerBelongsToOtherWorkspace_ThrowsWorkspaceAccessDeniedException()
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
            () => _sut.DeleteAsync(_userId, serverId, _workspaceId));
    }

    // ── Edge: Delete non-existent server ─────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenServerNotFound_ThrowsArgumentException()
    {
        _dataAccessMock
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((McpServer?)null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.DeleteAsync(_userId, Guid.NewGuid(), _workspaceId));
    }

    // ── Edge: Update with duplicate name on different server ─────────────────

    [Fact]
    public async Task UpdateAsync_WhenNewNameExistsOnDifferentServer_ThrowsValidationException()
    {
        var serverId = Guid.NewGuid();
        var server = new McpServerBuilder()
            .WithWorkspaceId(_workspaceId)
            .WithName("Old Name")
            .Build();

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(serverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(_workspaceId, "Taken Name", serverId, default))
            .ReturnsAsync(true);

        var request = new PatchMcpServerRequest(
            WorkspaceId: _workspaceId,
            Name: "Taken Name",
            TransportType: "HTTP",
            Http: new PatchHttpFields("https://api.example.com/mcp", "NONE", null),
            Stdio: null);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.UpdateAsync(_userId, serverId, request));
    }

    // ── Auth always checked before data access ────────────────────────────────

    [Fact]
    public async Task CreateAsync_AuthValidatedBeforeNameCheck()
    {
        var callOrder = new List<string>();

        _authMock
            .Setup(s => s.ValidateMembershipAsync(_userId, _workspaceId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("auth"))
            .Returns(Task.CompletedTask);

        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("nameCheck"))
            .ReturnsAsync(false);

        var request = new SaveMcpServerRequest(
            WorkspaceId: _workspaceId,
            Name: "Server",
            TransportType: "HTTP",
            Http: new SaveHttpFields("https://api.example.com/mcp", "NONE", null),
            Stdio: null);

        await _sut.CreateAsync(_userId, request);

        Assert.Equal(["auth", "nameCheck"], callOrder);
    }
}
