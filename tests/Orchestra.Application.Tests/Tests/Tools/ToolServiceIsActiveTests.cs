using NSubstitute;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tools.Services;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tools;

public class ToolServiceIsActiveTests
{
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess = Substitute.For<IToolCategoryDataAccess>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly IWorkspaceAuthorizationService _authService = Substitute.For<IWorkspaceAuthorizationService>();

    private ToolService CreateSut() => new(
        _toolCategoryDataAccess,
        _toolActionDataAccess,
        _agentToolActionDataAccess,
        _integrationDataAccess,
        _agentDataAccess,
        _authService);

    // ----------------------------------------------------------------
    // GetAvailableToolsAsync — deactivated MCP category excluded
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetAvailableToolsAsync_DeactivatedMcpCategory_NotIncludedInResult()
    {
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();

        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(workspaceId)
            .AsMcpBacked()
            .Build();

        var deactivatedCategory = ToolCategoryBuilder.DeactivatedMcpCategory(integrationId);

        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });
        _toolCategoryDataAccess.GetByProviderTypesAsync(Arg.Any<List<ProviderType>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolCategory>());
        _toolCategoryDataAccess.GetByIntegrationIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolCategory> { deactivatedCategory });
        _toolActionDataAccess.GetByCategoryIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        var sut = CreateSut();
        var result = await sut.GetAvailableToolsAsync(userId, workspaceId);

        Assert.Empty(result);
    }

    // ----------------------------------------------------------------
    // GetAgentToolActionsAsync — inactive tool action skipped
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolActionsAsync_InactiveToolAction_NotIncludedInResult()
    {
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var toolActionId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();
        var inactiveToolAction = new ToolActionBuilder()
            .WithId(toolActionId)
            .AsDeactivated()
            .Build();

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { toolActionId });
        _toolActionDataAccess.GetByIdAsync(toolActionId, Arg.Any<CancellationToken>())
            .Returns(inactiveToolAction);

        var sut = CreateSut();
        var result = await sut.GetAgentToolActionsAsync(userId, agentId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAgentToolActionsAsync_AgentWithZeroActiveToolsAfterCleanup_ReturnsEmptyList()
    {
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());

        var sut = CreateSut();
        var result = await sut.GetAgentToolActionsAsync(userId, agentId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAgentToolActionsAsync_MixOfActiveAndInactiveTools_ReturnsOnlyActive()
    {
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();
        var activeAction = new ToolActionBuilder().WithId(activeId).AsActive().Build();
        var inactiveAction = new ToolActionBuilder().WithId(inactiveId).AsDeactivated().Build();

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { activeId, inactiveId });
        _toolActionDataAccess.GetByIdAsync(activeId, Arg.Any<CancellationToken>()).Returns(activeAction);
        _toolActionDataAccess.GetByIdAsync(inactiveId, Arg.Any<CancellationToken>()).Returns(inactiveAction);

        var sut = CreateSut();
        var result = await sut.GetAgentToolActionsAsync(userId, agentId);

        Assert.Single(result);
        Assert.Equal(activeId, result[0].Id);
    }
}
