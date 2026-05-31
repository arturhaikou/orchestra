using NSubstitute;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentServiceTemplateImmutabilityTests
{
    private static (
        AgentService service,
        IAgentDataAccess agentDataAccess,
        IAgentToolActionDataAccess toolActionDataAccess)
        BuildSut()
    {
        var agentDataAccess = Substitute.For<IAgentDataAccess>();
        var toolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
        var authService = Substitute.For<IWorkspaceAuthorizationService>();
        var toolValidationService = Substitute.For<IToolValidationService>();

        toolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        toolActionDataAccess
            .GetUniqueCategoryNamesByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());
        toolActionDataAccess
            .ContainsReviewToolActionAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var service = new AgentService(
            agentDataAccess,
            toolActionDataAccess,
            Substitute.For<IAgentMcpToolDataAccess>(),
            Substitute.For<IAgentSubAgentDataAccess>(),
            Substitute.For<IAgentSkillDataAccess>(),
            Substitute.For<ISkillDataAccess>(),
            authService,
            toolValidationService,
            Substitute.For<IBuiltInAgentTemplateRegistry>(),
            Substitute.For<ITemplateAvailabilityResolver>(),
            Substitute.For<IToolActionDataAccess>(),
            Substitute.For<IIntegrationDataAccess>(),
            Substitute.For<IAgentSubAgentAssignmentService>());

        return (service, agentDataAccess, toolActionDataAccess);
    }

    [Fact]
    public async Task UpdateAgentAsync_TemplateAgent_PreservesTemplateIdentifierAndVersion()
    {
        var (sut, agentDataAccess, _) = BuildSut();
        var userId = Guid.NewGuid();
        var agent = new AgentBuilder()
            .WithName("Code Review Bot")
            .WithRole("Reviewer")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var updateRequest = new UpdateAgentRequest(
            Name: null,
            Role: null,
            Capabilities: null,
            ToolActionIds: null,
            CustomInstructions: "Updated instructions",
            ProjectPrinciples: null);

        var result = await sut.UpdateAgentAsync(userId, agent.Id, updateRequest);

        Assert.Equal("code-review", result.TemplateId);
        Assert.Equal(1, result.TemplateVersion);
        Assert.True(result.IsBuiltIn);
        Assert.Equal("Code Review Bot", result.Name);
    }

    [Fact]
    public async Task UpdateAgentAsync_CustomAgent_KeepsNullTemplateFields()
    {
        var (sut, agentDataAccess, _) = BuildSut();
        var userId = Guid.NewGuid();
        var agent = new AgentBuilder()
            .WithName("Custom Bot")
            .WithRole("Helper")
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var updateRequest = new UpdateAgentRequest(
            Name: "Renamed Custom Bot",
            Role: null,
            Capabilities: null,
            ToolActionIds: null,
            CustomInstructions: null,
            ProjectPrinciples: null);

        var result = await sut.UpdateAgentAsync(userId, agent.Id, updateRequest);

        Assert.Null(result.TemplateId);
        Assert.Null(result.TemplateVersion);
        Assert.False(result.IsBuiltIn);
    }
}