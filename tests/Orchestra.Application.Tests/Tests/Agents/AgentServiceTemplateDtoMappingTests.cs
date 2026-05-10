using NSubstitute;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentServiceTemplateDtoMappingTests
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

        var service = new AgentService(
            agentDataAccess,
            toolActionDataAccess,
            Substitute.For<IAgentMcpToolDataAccess>(),
            authService,
            toolValidationService,
            Substitute.For<IBuiltInAgentTemplateRegistry>(),
            Substitute.For<ITemplateAvailabilityResolver>(),
            Substitute.For<IToolActionDataAccess>(),
            Substitute.For<IIntegrationDataAccess>());

        return (service, agentDataAccess, toolActionDataAccess);
    }

    [Fact]
    public async Task GetAgentByIdAsync_CustomAgent_ReturnsDtoWithNullTemplateFieldsAndIsBuiltInFalse()
    {
        var (sut, agentDataAccess, _) = BuildSut();
        var userId = Guid.NewGuid();
        var agent = new AgentBuilder().Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var result = await sut.GetAgentByIdAsync(userId, agent.Id);

        Assert.Null(result.TemplateIdentifier);
        Assert.Null(result.TemplateVersion);
        Assert.False(result.IsBuiltIn);
    }

    [Fact]
    public async Task GetAgentByIdAsync_TemplateAgent_ReturnsDtoWithTemplateFieldsAndIsBuiltInTrue()
    {
        var (sut, agentDataAccess, _) = BuildSut();
        var userId = Guid.NewGuid();
        var agent = new AgentBuilder()
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var result = await sut.GetAgentByIdAsync(userId, agent.Id);

        Assert.Equal("code-review", result.TemplateIdentifier);
        Assert.Equal(1, result.TemplateVersion);
        Assert.True(result.IsBuiltIn);
    }
}