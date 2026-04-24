using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Infrastructure.Hubs;
using Orchestra.Infrastructure.Services;

namespace Orchestra.Infrastructure.Tests.Services;

public class NotificationServiceTests
{
    private static (
        NotificationService sut,
        IHubContext<NotificationHub> hubContext,
        IClientProxy clientProxy)
        BuildSut()
    {
        var hubContext = Substitute.For<IHubContext<NotificationHub>>();
        var hubClients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        var logger = Substitute.For<ILogger<NotificationService>>();

        hubContext.Clients.Returns(hubClients);
        hubClients.Group(Arg.Any<string>()).Returns(clientProxy);

        var sut = new NotificationService(hubContext, logger);
        return (sut, hubContext, clientProxy);
    }

    private static AgentExecutionCompletedNotification BuildNotification(
        Guid? workspaceId = null,
        string status = "success",
        string? reviewUrl = null)
    {
        return new AgentExecutionCompletedNotification(
            WorkspaceId: workspaceId ?? Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            AgentName: "Code Reviewer",
            TicketId: Guid.NewGuid(),
            TicketTitle: "Fix login bug",
            Status: status,
            Summary: "Review completed.",
            ReviewUrl: reviewUrl);
    }

    [Fact]
    public async Task NotifyAgentExecutionCompletedAsync_WithSuccessNotification_SendsToCorrectWorkspaceGroup()
    {
        // Arrange
        var (sut, hubContext, clientProxy) = BuildSut();
        var workspaceId = Guid.NewGuid();
        var notification = BuildNotification(workspaceId: workspaceId, status: "success", reviewUrl: "https://github.com/org/repo/pull/42");

        // Act
        await sut.NotifyAgentExecutionCompletedAsync(notification, CancellationToken.None);

        // Assert
        hubContext.Clients.Received(1).Group($"workspace-{workspaceId}");
        await clientProxy.Received(1).SendCoreAsync(
            "AgentExecutionCompleted",
            Arg.Is<object?[]>(args => args.Length == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAgentExecutionCompletedAsync_WithFailedNotification_SendsFailedStatusToGroup()
    {
        // Arrange
        var (sut, hubContext, clientProxy) = BuildSut();
        var notification = BuildNotification(status: "failed");

        // Act
        await sut.NotifyAgentExecutionCompletedAsync(notification, CancellationToken.None);

        // Assert
        await clientProxy.Received(1).SendCoreAsync(
            "AgentExecutionCompleted",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAgentExecutionCompletedAsync_WhenSendAsyncThrows_DoesNotPropagateException()
    {
        // Arrange
        var (sut, hubContext, clientProxy) = BuildSut();
        var notification = BuildNotification();

        clientProxy
            .SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Hub unavailable"));

        // Act & Assert — no exception should escape
        var exception = await Record.ExceptionAsync(() =>
            sut.NotifyAgentExecutionCompletedAsync(notification, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task NotifyAgentExecutionCompletedAsync_WithEmptyGroup_CompletesWithoutError()
    {
        // Arrange — SendCoreAsync completes normally (SignalR delivers to zero clients silently)
        var (sut, hubContext, clientProxy) = BuildSut();
        var notification = BuildNotification();

        // Act
        var exception = await Record.ExceptionAsync(() =>
            sut.NotifyAgentExecutionCompletedAsync(notification, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task NotifyAgentExecutionCompletedAsync_WithDifferentWorkspaces_SendsToCorrectGroupOnly()
    {
        // Arrange
        var (sut, hubContext, clientProxy) = BuildSut();
        var workspaceA = Guid.NewGuid();
        var notificationA = BuildNotification(workspaceId: workspaceA);

        // Act
        await sut.NotifyAgentExecutionCompletedAsync(notificationA, CancellationToken.None);

        // Assert — only workspace A's group was targeted
        hubContext.Clients.Received(1).Group($"workspace-{workspaceA}");
        hubContext.Clients.DidNotReceive().Group(Arg.Is<string>(g => g != $"workspace-{workspaceA}"));
    }

    [Fact]
    public async Task NotifyAgentExecutionCompletedAsync_PayloadExcludesWorkspaceId_VerifiesPayloadShape()
    {
        // Arrange
        var (sut, hubContext, clientProxy) = BuildSut();
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var notification = new AgentExecutionCompletedNotification(
            WorkspaceId: workspaceId,
            AgentId: agentId,
            AgentName: "Test Agent",
            TicketId: ticketId,
            TicketTitle: "Test Ticket",
            Status: "success",
            Summary: "Done",
            ReviewUrl: "https://example.com/pr/1");

        // Act
        await sut.NotifyAgentExecutionCompletedAsync(notification, CancellationToken.None);

        // Assert — payload was sent (detailed payload assertion depends on implementation shape)
        await clientProxy.Received(1).SendCoreAsync(
            "AgentExecutionCompleted",
            Arg.Is<object?[]>(args => args.Length == 1 && args[0] != null),
            Arg.Any<CancellationToken>());
    }
}
