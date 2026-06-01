using NSubstitute;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentServiceGetAgentsModelTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (
        AgentService service,
        IAgentDataAccess agentDataAccess,
        IWorkspaceAuthorizationService authService,
        IAgentToolActionDataAccess toolActionDataAccess,
        IToolValidationService toolValidationService)
        BuildSut()
    {
        var agentDataAccess = Substitute.For<IAgentDataAccess>();
        var authService = Substitute.For<IWorkspaceAuthorizationService>();
        var toolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
        var toolValidationService = Substitute.For<IToolValidationService>();

        // Default stubs so MapToDtoAsync does not throw
        toolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        toolActionDataAccess
            .GetUniqueCategoryNamesByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        var service = new AgentService(
            agentDataAccess,
            toolActionDataAccess,
            Substitute.For<IAgentMcpToolDataAccess>(),
            Substitute.For<IAgentSubAgentDataAccess>(),
            Substitute.For<IAgentSkillDataAccess>(),
            Substitute.For<IAgentSkillFolderDataAccess>(),
            Substitute.For<ISkillDataAccess>(),
            Substitute.For<ISkillFolderDataAccess>(),
            authService,
            toolValidationService,
            Substitute.For<IBuiltInAgentTemplateRegistry>(),
            Substitute.For<ITemplateAvailabilityResolver>(),
            Substitute.For<IToolActionDataAccess>(),
            Substitute.For<IIntegrationDataAccess>(),
            Substitute.For<IAgentSubAgentAssignmentService>());

        return (service, agentDataAccess, authService, toolActionDataAccess, toolValidationService);
    }

    // -------------------------------------------------------------------------
    // Scenario 1: Agent with a stored model value → DTO includes that value
    // AC: "agent A has a stored model value of 'gpt-4o' → card displays 'gpt-4o'"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentsByWorkspaceIdAsync_WhenAgentHasStoredModel_ReturnsDtoWithThatModel()
    {
        // Arrange
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithModel("gpt-4o")
            .Build();

        agentDataAccess
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Agent> { agent });

        // Act
        var result = await sut.GetAgentsByWorkspaceIdAsync(userId, workspaceId);

        // Assert
        Assert.Single(result);
        Assert.Equal("gpt-4o", result[0].Model);
    }

    // -------------------------------------------------------------------------
    // Scenario 2 & 4: Agent with null model → DTO includes null
    // AC: "agent A has no stored model value (null) → card displays 'Default'"
    // Note: null-to-"Default" is the UI presentation rule; the DTO carries null.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentsByWorkspaceIdAsync_WhenAgentHasNullModel_ReturnsDtoWithNullModel()
    {
        // Arrange
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithModel(null)
            .Build();

        agentDataAccess
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Agent> { agent });

        // Act
        var result = await sut.GetAgentsByWorkspaceIdAsync(userId, workspaceId);

        // Assert
        Assert.Single(result);
        Assert.Null(result[0].Model);
    }

    // -------------------------------------------------------------------------
    // Scenario 3: Mixed agents → each DTO carries its own model value
    // AC: one with "gpt-4o", one with "llama3", one with null → all projected correctly
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentsByWorkspaceIdAsync_WhenWorkspaceHasMixedAgents_ProjectsModelForEach()
    {
        // Arrange
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var agentGpt = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithModel("gpt-4o")
            .Build();

        var agentLlama = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithModel("llama3")
            .Build();

        var agentDefault = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithModel(null)
            .Build();

        agentDataAccess
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Agent> { agentGpt, agentLlama, agentDefault });

        // Act
        var result = await sut.GetAgentsByWorkspaceIdAsync(userId, workspaceId);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, dto => dto.Model == "gpt-4o");
        Assert.Contains(result, dto => dto.Model == "llama3");
        Assert.Contains(result, dto => dto.Model == null);
    }
}
