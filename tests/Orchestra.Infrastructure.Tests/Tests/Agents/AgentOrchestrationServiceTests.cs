using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Infrastructure.Agents;

namespace Orchestra.Infrastructure.Tests.Tests.Agents;

public class AgentOrchestrationServiceTests
{
    private static readonly Guid ToDoStatusId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid InProgressStatusId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid CompletedStatusId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private readonly IAgentRuntimeService _agentRuntimeService = Substitute.For<IAgentRuntimeService>();
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly ITicketDataAccess _ticketDataAccess = Substitute.For<ITicketDataAccess>();
    private readonly IAgentContextBuilder _agentContextBuilder = Substitute.For<IAgentContextBuilder>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly ILogger<AgentOrchestrationService> _logger = Substitute.For<ILogger<AgentOrchestrationService>>();
    private readonly AgentOrchestrationService _sut;

    public AgentOrchestrationServiceTests()
    {
        _sut = new AgentOrchestrationService(
            _agentRuntimeService,
            _agentDataAccess,
            _ticketDataAccess,
            _agentContextBuilder,
            _notificationService,
            _logger);
    }

    private (Guid ticketId, Guid workspaceId, Guid agentId) SetupSuccessfulExecution()
    {
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var ticket = new TicketBuilder()
            .WithWorkspaceId(workspaceId)
            .WithStatusId(ToDoStatusId)
            .Build();
        ticket.UpdateAssignments(agentId, null, null, null);
        var ticketId = ticket.Id;

        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _agentContextBuilder
            .BuildAgentContextWithIntegrationsAsync(ticket, agent, Arg.Any<CancellationToken>())
            .Returns("context prompt");
        _agentRuntimeService
            .ExecuteAgentAsync(agentId, "context prompt", agent.Model, agent.ProjectPrinciples, null, Arg.Any<CancellationToken>())
            .Returns("response");

        return (ticketId, workspaceId, agentId);
    }

    [Fact]
    public async Task ExecuteAgentForTicketAsync_DispatchesInProgressNotification_WhenStatusChangesToInProgress()
    {
        var (ticketId, workspaceId, _) = SetupSuccessfulExecution();

        await _sut.ExecuteAgentForTicketAsync(ticketId);

        await _notificationService.Received().NotifyTicketStatusChangedAsync(
            Arg.Is<TicketStatusChangedNotification>(n =>
                n.WorkspaceId == workspaceId &&
                n.TicketId == ticketId &&
                n.NewStatus == "In Progress" &&
                n.PreviousStatus == "To Do"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAgentForTicketAsync_DispatchesCompletedNotification_OnSuccessfulExecution()
    {
        var (ticketId, workspaceId, _) = SetupSuccessfulExecution();

        await _sut.ExecuteAgentForTicketAsync(ticketId);

        await _notificationService.Received().NotifyTicketStatusChangedAsync(
            Arg.Is<TicketStatusChangedNotification>(n =>
                n.WorkspaceId == workspaceId &&
                n.TicketId == ticketId &&
                n.NewStatus == "Completed" &&
                n.PreviousStatus == "In Progress"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAgentForTicketAsync_DispatchesToDoRevertNotification_OnExecutionFailure()
    {
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var ticket = new TicketBuilder()
            .WithWorkspaceId(workspaceId)
            .WithStatusId(ToDoStatusId)
            .Build();
        ticket.UpdateAssignments(agentId, null, null, null);
        var ticketId = ticket.Id;

        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _agentContextBuilder
            .BuildAgentContextWithIntegrationsAsync(ticket, agent, Arg.Any<CancellationToken>())
            .Returns("context prompt");
        _agentRuntimeService
            .ExecuteAgentAsync(agentId, "context prompt", agent.Model, agent.ProjectPrinciples, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("AI execution failed"));

        await _sut.ExecuteAgentForTicketAsync(ticketId);

        await _notificationService.Received().NotifyTicketStatusChangedAsync(
            Arg.Is<TicketStatusChangedNotification>(n =>
                n.WorkspaceId == workspaceId &&
                n.TicketId == ticketId &&
                n.NewStatus == "To Do" &&
                n.PreviousStatus == "In Progress"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAgentForTicketAsync_DispatchesTwoStatusNotifications_OnSuccessfulExecution()
    {
        var (ticketId, _, _) = SetupSuccessfulExecution();

        await _sut.ExecuteAgentForTicketAsync(ticketId);

        await _notificationService.Received(2).NotifyTicketStatusChangedAsync(
            Arg.Any<TicketStatusChangedNotification>(),
            Arg.Any<CancellationToken>());
    }
}
