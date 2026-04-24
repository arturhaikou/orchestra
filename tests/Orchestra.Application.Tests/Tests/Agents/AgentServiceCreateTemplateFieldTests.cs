using NSubstitute;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentServiceCreateTemplateFieldTests
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
            authService,
            toolValidationService,
            Substitute.For<IBuiltInAgentTemplateRegistry>(),
            Substitute.For<ITemplateAvailabilityResolver>(),
            Substitute.For<IToolActionDataAccess>(),
            Substitute.For<IIntegrationDataAccess>());

        return (service, agentDataAccess, toolActionDataAccess);
    }

    [Fact]
    public async Task CreateAgentAsync_StandardAgent_HasNullTemplateFieldsAndIsBuiltInFalse()
    {
        var (sut, agentDataAccess, _) = BuildSut();
        var userId = Guid.NewGuid();

        var request = new CreateAgentRequest(
            WorkspaceId: Guid.NewGuid(),
            Name: "Test Agent",
            Role: "Helper",
            Capabilities: new[] { "analysis" },
            ToolActionIds: null,
            CustomInstructions: "Do helpful things",
            ProjectPrinciples: null,
            Model: null);

        var result = await sut.CreateAgentAsync(userId, request);

        Assert.Null(result.TemplateIdentifier);
        Assert.Null(result.TemplateVersion);
        Assert.False(result.IsBuiltIn);
    }
}