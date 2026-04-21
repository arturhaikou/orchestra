using Orchestra.Infrastructure.Hubs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;

namespace Orchestra.ApiService.Tests.Tests.Hubs;

public class NotificationHubTests
{
    private readonly IWorkspaceAuthorizationService _mockAuthService;
    private readonly IGroupManager _mockGroupManager;
    private readonly HubCallerContext _mockContext;
    private readonly NotificationHub _hub;

    private readonly Guid _userId = Guid.NewGuid();
    private const string ConnectionId = "test-connection-id-123";

    public NotificationHubTests()
    {
        _mockAuthService = Substitute.For<IWorkspaceAuthorizationService>();
        _mockGroupManager = Substitute.For<IGroupManager>();
        _mockContext = Substitute.For<HubCallerContext>();

        _mockContext.ConnectionId.Returns(ConnectionId);
        _mockContext.ConnectionAborted.Returns(CancellationToken.None);

        var claimsIdentity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        });
        _mockContext.User.Returns(new ClaimsPrincipal(claimsIdentity));

        _hub = new NotificationHub(_mockAuthService, NullLogger<NotificationHub>.Instance)
        {
            Context = _mockContext,
            Groups = _mockGroupManager
        };
    }

    // ---------------------------------------------------------------
    // JoinWorkspaceGroup — Scenario 1
    // ---------------------------------------------------------------

    [Fact]
    public async Task JoinWorkspaceGroup_WhenUserIsMember_AddsConnectionToGroup()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var expectedGroupName = $"workspace-{workspaceId}";

        _mockAuthService
            .EnsureUserIsMemberAsync(_userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _hub.JoinWorkspaceGroup(workspaceId);

        // Assert
        await _mockGroupManager.Received(1)
            .AddToGroupAsync(ConnectionId, expectedGroupName, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // JoinWorkspaceGroup — Scenario 2
    // ---------------------------------------------------------------

    [Fact]
    public async Task JoinWorkspaceGroup_WhenUserIsNotMember_ThrowsHubExceptionWithAccessDeniedMessage()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        _mockAuthService
            .EnsureUserIsMemberAsync(_userId, workspaceId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(_userId, workspaceId));

        // Act
        var exception = await Assert.ThrowsAsync<HubException>(
            () => _hub.JoinWorkspaceGroup(workspaceId));

        // Assert
        Assert.Equal("Access denied.", exception.Message);
        await _mockGroupManager.DidNotReceive()
            .AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinWorkspaceGroup_WhenUserIsNotMember_DoesNotAddConnectionToGroup()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        _mockAuthService
            .EnsureUserIsMemberAsync(_userId, workspaceId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(_userId, workspaceId));

        // Act
        await Assert.ThrowsAsync<HubException>(
            () => _hub.JoinWorkspaceGroup(workspaceId));

        // Assert — connection was never added to any group
        await _mockGroupManager.DidNotReceive()
            .AddToGroupAsync(ConnectionId, $"workspace-{workspaceId}", Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // LeaveWorkspaceGroup — Scenario 4
    // ---------------------------------------------------------------

    [Fact]
    public async Task LeaveWorkspaceGroup_WhenCalled_RemovesConnectionFromGroup()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var expectedGroupName = $"workspace-{workspaceId}";

        // Act
        await _hub.LeaveWorkspaceGroup(workspaceId);

        // Assert
        await _mockGroupManager.Received(1)
            .RemoveFromGroupAsync(ConnectionId, expectedGroupName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveWorkspaceGroup_WhenCalled_DoesNotCheckMembership()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        await _hub.LeaveWorkspaceGroup(workspaceId);

        // Assert — no membership check is performed on leave
        await _mockAuthService.DidNotReceive()
            .EnsureUserIsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _mockAuthService.DidNotReceive()
            .ValidateMembershipAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // JoinWorkspaceGroup — group naming convention
    // ---------------------------------------------------------------

    [Fact]
    public async Task JoinWorkspaceGroup_WhenUserIsMember_UsesCorrectGroupNameFormat()
    {
        // Arrange
        var workspaceId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var expectedGroupName = "workspace-3fa85f64-5717-4562-b3fc-2c963f66afa6";

        _mockAuthService
            .EnsureUserIsMemberAsync(_userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _hub.JoinWorkspaceGroup(workspaceId);

        // Assert — exact group name format used
        await _mockGroupManager.Received(1)
            .AddToGroupAsync(ConnectionId, expectedGroupName, Arg.Any<CancellationToken>());
    }
}
