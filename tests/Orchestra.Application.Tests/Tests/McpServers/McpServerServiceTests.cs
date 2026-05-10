using Moq;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers;
using Orchestra.Application.McpServers.Interfaces;

namespace Orchestra.Application.Tests.Tests.McpServers;

public class McpServerServiceTests
{
    private readonly Mock<IWorkspaceAuthorizationService> _workspaceAuthMock;
    private readonly Mock<IMcpServerDataAccess> _dataAccessMock;
    private readonly McpServerService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public McpServerServiceTests()
    {
        _workspaceAuthMock = new Mock<IWorkspaceAuthorizationService>();
        _dataAccessMock = new Mock<IMcpServerDataAccess>();

        _workspaceAuthMock
            .Setup(s => s.ValidateMembershipAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _sut = new McpServerService(_workspaceAuthMock.Object, _dataAccessMock.Object);
    }

    // ── Scenario 1: Unique name returns true ─────────────────────────────────

    [Fact]
    public async Task IsNameUniqueAsync_WhenNameDoesNotExist_ReturnsTrue()
    {
        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(_workspaceId, "New Server", null, default))
            .ReturnsAsync(false);

        var result = await _sut.IsNameUniqueAsync(_userId, _workspaceId, "New Server", null);

        Assert.True(result);
    }

    // ── Scenario 3 / Duplicate Name ──────────────────────────────────────────

    [Fact]
    public async Task IsNameUniqueAsync_WhenNameAlreadyExists_ReturnsFalse()
    {
        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(_workspaceId, "Figma Tools", null, default))
            .ReturnsAsync(true);

        var result = await _sut.IsNameUniqueAsync(_userId, _workspaceId, "Figma Tools", null);

        Assert.False(result);
    }

    // ── Scenario 7: Edit-mode excludeId is forwarded to data access ──────────

    [Fact]
    public async Task IsNameUniqueAsync_InEditMode_PassesExcludeIdToDataAccess()
    {
        var ownId = Guid.NewGuid();

        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(_workspaceId, "My Server", ownId, default))
            .ReturnsAsync(false);

        var result = await _sut.IsNameUniqueAsync(_userId, _workspaceId, "My Server", ownId);

        Assert.True(result);
        _dataAccessMock.Verify(
            s => s.ExistsByNameAsync(_workspaceId, "My Server", ownId, default),
            Times.Once);
    }

    // ── Authorization: ValidateMembership is always called first ─────────────

    [Fact]
    public async Task IsNameUniqueAsync_AlwaysValidatesMembershipBeforeQuery()
    {
        var callOrder = new List<string>();

        _workspaceAuthMock
            .Setup(s => s.ValidateMembershipAsync(_userId, _workspaceId, default))
            .Callback(() => callOrder.Add("auth"))
            .Returns(Task.CompletedTask);

        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(_workspaceId, "Any Name", null, default))
            .Callback(() => callOrder.Add("query"))
            .ReturnsAsync(false);

        await _sut.IsNameUniqueAsync(_userId, _workspaceId, "Any Name", null);

        Assert.Equal(new[] { "auth", "query" }, callOrder);
    }

    // ── Authorization: Unauthorized exception propagates ─────────────────────

    [Fact]
    public async Task IsNameUniqueAsync_WhenUserNotWorkspaceMember_ThrowsUnauthorized()
    {
        _workspaceAuthMock
            .Setup(s => s.ValidateMembershipAsync(_userId, _workspaceId, default))
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(_userId, _workspaceId));

        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => _sut.IsNameUniqueAsync(_userId, _workspaceId, "Some Name", null));
    }

    // ── Authorization: Data access NOT called when auth fails ────────────────

    [Fact]
    public async Task IsNameUniqueAsync_WhenAuthFails_DataAccessIsNotCalled()
    {
        _workspaceAuthMock
            .Setup(s => s.ValidateMembershipAsync(_userId, _workspaceId, default))
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(_userId, _workspaceId));

        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => _sut.IsNameUniqueAsync(_userId, _workspaceId, "Some Name", null));

        _dataAccessMock.Verify(
            s => s.ExistsByNameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Cancellation token is forwarded ──────────────────────────────────────

    [Fact]
    public async Task IsNameUniqueAsync_ForwardsCancellationTokenToDataAccess()
    {
        var cts = new CancellationTokenSource();

        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(_workspaceId, "Server", null, cts.Token))
            .ReturnsAsync(false);

        await _sut.IsNameUniqueAsync(_userId, _workspaceId, "Server", null, cts.Token);

        _dataAccessMock.Verify(
            s => s.ExistsByNameAsync(_workspaceId, "Server", null, cts.Token),
            Times.Once);
    }
}
