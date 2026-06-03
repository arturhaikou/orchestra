using Microsoft.Extensions.Logging;
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

/// <summary>
/// Unit tests verifying that AgentOrchestrationService correctly forwards the agent's
/// stored model identifier to IAgentRuntimeService.ExecuteAgentAsync (FR-04).
/// </summary>
public class AgentOrchestrationServiceModelRoutingTests
{
    // Seeded status IDs (must match values in AgentOrchestrationService)
    private static readonly Guid ToDoStatusId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (
        AgentOrchestrationService sut,
        IAgentRuntimeService runtimeService,
        IAgentDataAccess agentDataAccess,
        ITicketDataAccess ticketDataAccess,
        IJobService jobService,
        IAgentContextBuilder contextBuilder)
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

        // Default stub: runtime service returns a successful response text and job ID
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

        // Default stub: context builder returns a fully enriched prompt
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

        return (sut, runtimeService, agentDataAccess, ticketDataAccess, jobService, contextBuilder);
    }

    // -------------------------------------------------------------------------
    // Scenario 1: Agent has a configured model → model identifier is forwarded
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAgentForTicketAsync_WhenAgentHasConfiguredModel_PassesModelToRuntimeService()
    {
        // Arrange
        var (sut, runtimeService, agentDataAccess, ticketDataAccess, _, _) = BuildSut();

        const string agentModel = "gpt-4o";
        var workspaceId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithModel(agentModel)
            .Build();

        // Build an internal ticket then assign the agent (UpdateAssignments accepts null
        // for agentWorkspaceId to skip workspace validation in unit tests)
        var ticket = new TicketBuilder()
            .WithWorkspaceId(workspaceId)
            .WithStatusId(ToDoStatusId)
            .Build();
        ticket.UpdateAssignments(agent.Id, null, null, null);

        ticketDataAccess
            .GetTicketByIdAsync(ticket.Id, Arg.Any<CancellationToken>())
            .Returns(ticket);
        agentDataAccess
            .GetByIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns(agent);

        // Act
        await sut.ExecuteAgentForTicketAsync(ticket.Id);

        // Assert — ExecuteAgentAsync must be called exactly once with the agent's model
        await runtimeService.Received(1).ExecuteAgentAsync(
            agent.Id,
            Arg.Any<AgentContextInput>(),
            agentModel,
            Arg.Any<string?>(),
            Arg.Any<JobContext?>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 2: Agent has no model (null) → null is forwarded (system default)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAgentForTicketAsync_WhenAgentHasNoModel_PassesNullModelToRuntimeService()
    {
        // Arrange
        var (sut, runtimeService, agentDataAccess, ticketDataAccess, _, _) = BuildSut();

        var workspaceId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithModel(null)   // No model override — should fall back to system default
            .Build();

        var ticket = new TicketBuilder()
            .WithWorkspaceId(workspaceId)
            .WithStatusId(ToDoStatusId)
            .Build();
        ticket.UpdateAssignments(agent.Id, null, null, null);

        ticketDataAccess
            .GetTicketByIdAsync(ticket.Id, Arg.Any<CancellationToken>())
            .Returns(ticket);
        agentDataAccess
            .GetByIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns(agent);

        // Act
        await sut.ExecuteAgentForTicketAsync(ticket.Id);

        // Assert — ExecuteAgentAsync must be called with null agentModel
        await runtimeService.Received(1).ExecuteAgentAsync(
            agent.Id,
            Arg.Any<AgentContextInput>(),
            null,
            Arg.Any<string?>(),
            Arg.Any<JobContext?>(),
            Arg.Any<CancellationToken>());
    }
}
