using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Models;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Jobs.Services;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Agents;

namespace Orchestra.Infrastructure.Tests.Agents;

public class AgentOrchestrationServiceNotificationTests
{
    private static readonly Guid ToDoStatusId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static (
        AgentOrchestrationService sut,
        IAgentRuntimeService runtimeService,
        IAgentDataAccess agentDataAccess,
        ITicketDataAccess ticketDataAccess,
        IJobService jobService,
        IAgentContextBuilder contextBuilder,
        INotificationService notificationService)
        BuildSut()
    {
        var runtimeService = Substitute.For<IAgentRuntimeService>();
        var agentDataAccess = Substitute.For<IAgentDataAccess>();
        var ticketDataAccess = Substitute.For<ITicketDataAccess>();
        var jobService = Substitute.For<IJobService>();
        var contextBuilder = Substitute.For<IAgentContextBuilder>();
        var notificationService = Substitute.For<INotificationService>();
        var workflowEngine = Substitute.For<IWorkflowExecutionEngine>();
        var ticketIdParsingService = Substitute.For<ITicketIdParsingService>();
        var logger = Substitute.For<ILogger<AgentOrchestrationService>>();

        var jobId = Guid.NewGuid();
        runtimeService
            .ExecuteAgentAsync(
                Arg.Any<Guid>(),
                Arg.Any<AgentContextInput>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<JobContext?>(),
                Arg.Any<CancellationToken>())
            .Returns(("Agent response", jobId));

        jobService
            .GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(new JobDetailDto(
                Id: jobId,
                WorkspaceId: Guid.Empty,
                AgentId: Guid.Empty,
                AgentName: "Test Agent",
                TicketTitle: "Test Ticket",
                TicketId: Guid.Empty,
                Status: JobStatus.Completed,
                TriggerType: JobTriggerType.Ticket,
                CreatedAt: DateTime.UtcNow,
                StartedAt: null,
                CompletedAt: null,
                InitialPrompt: "prompt",
                FinalResponse: "response",
                ErrorMessage: null,
                Steps: new List<JobStepDto>()));

        contextBuilder
            .BuildAgentContextWithIntegrationsAsync(
                Arg.Any<Ticket>(),
                Arg.Any<Agent>(),
                Arg.Any<CancellationToken>())
            .Returns(AgentContextInput.TextOnly("Enriched context prompt"));

        var sut = new AgentOrchestrationService(
            runtimeService,
            agentDataAccess,
            ticketDataAccess,
            jobService,
            contextBuilder,
            notificationService,
            workflowEngine,
            ticketIdParsingService,
            logger);

        return (sut, runtimeService, agentDataAccess, ticketDataAccess, jobService, contextBuilder, notificationService);
    }

    private static void SetupTicketAndAgent(
        ITicketDataAccess ticketDataAccess,
        IAgentDataAccess agentDataAccess,
        out Guid ticketId,
        out Guid agentId,
        out Guid workspaceId)
    {
        workspaceId = Guid.NewGuid();
        agentId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithName("Test Agent")
            .WithWorkspaceId(workspaceId)
            .Build();

        var ticket = new TicketBuilder()
            .WithTitle("Test Ticket")
            .WithWorkspaceId(workspaceId)
            .WithStatusId(ToDoStatusId)
            .Build();
        ticket.UpdateAssignments(agentId, null, null, null);
        ticketId = ticket.Id;

        ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>())
            .Returns(ticket);
        agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
    }

    [Fact]
    public async Task ExecuteAgentForTicketAsync_OnSuccess_SendsNotificationWithSuccessStatus()
    {
        // Arrange
        var (sut, _, agentDataAccess, ticketDataAccess, _, _, notificationService) = BuildSut();
        SetupTicketAndAgent(ticketDataAccess, agentDataAccess, out var ticketId, out _, out var workspaceId);

        // Act
        var result = await sut.ExecuteAgentForTicketAsync(ticketId);

        // Assert
        Assert.True(result.IsSuccess);
        await notificationService.Received(1).NotifyAgentExecutionCompletedAsync(
            Arg.Is<AgentExecutionCompletedNotification>(n =>
                n.WorkspaceId == workspaceId &&
                n.Status == "success" &&
                n.TicketId == ticketId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAgentForTicketAsync_OnFailure_SendsNotificationWithFailedStatus()
    {
        // Arrange
        var (sut, runtimeService, agentDataAccess, ticketDataAccess, _, _, notificationService) = BuildSut();
        SetupTicketAndAgent(ticketDataAccess, agentDataAccess, out var ticketId, out _, out var workspaceId);

        runtimeService
            .ExecuteAgentAsync(
                Arg.Any<Guid>(),
                Arg.Any<AgentContextInput>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<JobContext?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM timeout"));

        // Act
        var result = await sut.ExecuteAgentForTicketAsync(ticketId);

        // Assert
        Assert.False(result.IsSuccess);
        await notificationService.Received(1).NotifyAgentExecutionCompletedAsync(
            Arg.Is<AgentExecutionCompletedNotification>(n =>
                n.WorkspaceId == workspaceId &&
                n.Status == "failed" &&
                n.TicketId == ticketId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAgentForTicketAsync_WhenNotificationThrows_ReturnsResultUnchanged()
    {
        // Arrange
        var (sut, _, agentDataAccess, ticketDataAccess, _, _, notificationService) = BuildSut();
        SetupTicketAndAgent(ticketDataAccess, agentDataAccess, out var ticketId, out _, out _);

        notificationService
            .NotifyAgentExecutionCompletedAsync(
                Arg.Any<AgentExecutionCompletedNotification>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SignalR down"));

        // Act
        var result = await sut.ExecuteAgentForTicketAsync(ticketId);

        // Assert — execution result is still success despite notification failure
        Assert.True(result.IsSuccess);
        Assert.Equal("Agent response", result.Message);
    }

    [Fact]
    public async Task ExecuteAgentForTicketAsync_OnSuccess_NotificationContainsAgentNameAndTicketTitle()
    {
        // Arrange
        var (sut, _, agentDataAccess, ticketDataAccess, _, _, notificationService) = BuildSut();
        SetupTicketAndAgent(ticketDataAccess, agentDataAccess, out var ticketId, out _, out _);

        // Act
        await sut.ExecuteAgentForTicketAsync(ticketId);

        // Assert
        await notificationService.Received(1).NotifyAgentExecutionCompletedAsync(
            Arg.Is<AgentExecutionCompletedNotification>(n =>
                n.AgentName == "Test Agent" &&
                n.TicketTitle == "Test Ticket"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAgentForTicketAsync_WhenTicketNotFound_DoesNotSendNotification()
    {
        // Arrange
        var (sut, _, _, ticketDataAccess, _, _, notificationService) = BuildSut();
        var ticketId = Guid.NewGuid();
        ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>())
            .Returns((Ticket?)null);

        // Act
        var result = await sut.ExecuteAgentForTicketAsync(ticketId);

        // Assert
        Assert.False(result.IsSuccess);
        await notificationService.DidNotReceive().NotifyAgentExecutionCompletedAsync(
            Arg.Any<AgentExecutionCompletedNotification>(),
            Arg.Any<CancellationToken>());
    }
}
