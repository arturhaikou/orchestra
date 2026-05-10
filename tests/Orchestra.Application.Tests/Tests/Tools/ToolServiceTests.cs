using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tools.DTOs;
using Orchestra.Application.Tools.Services;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tools;

public class ToolServiceTests
{
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess = Substitute.For<IToolCategoryDataAccess>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly ToolService _sut;

    public ToolServiceTests()
    {
        _sut = new ToolService(
            _toolCategoryDataAccess,
            _toolActionDataAccess,
            _agentToolActionDataAccess,
            _integrationDataAccess,
            _agentDataAccess,
            _workspaceAuthorizationService);
    }

    [Fact]
    public async Task ToggleToolActionEnabledAsync_WithDestructiveTool_EnablesTool()
    {
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var toolCategoryId = Guid.NewGuid();
        var toolActionId = Guid.NewGuid();

        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .Build();

        var toolCategory = new ToolCategoryBuilder()
            .WithIntegrationId(integrationId)
            .Build();

        var toolAction = new ToolActionBuilder()
            .WithId(toolActionId)
            .WithToolCategoryId(toolCategoryId)
            .WithDangerLevel(DangerLevel.Destructive)
            .AsMcpTool(integrationId)
            .WithIsEnabled(false)
            .Build();

        _toolActionDataAccess.GetByIdAsync(toolActionId, Arg.Any<CancellationToken>())
            .Returns(toolAction);
        _toolCategoryDataAccess.GetByIdAsync(toolAction.ToolCategoryId, Arg.Any<CancellationToken>())
            .Returns(toolCategory);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);

        var result = await _sut.ToggleToolActionEnabledAsync(userId, toolActionId, true);

        Assert.True(result.IsEnabled);
        await _toolActionDataAccess.Received(1).UpdateAsync(
            Arg.Is<ToolAction>(ta => ta.IsEnabled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleToolActionEnabledAsync_WithEnabledTool_DisablesTool()
    {
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var toolActionId = Guid.NewGuid();

        var integration = new IntegrationBuilder().WithId(integrationId).Build();
        var toolCategory = new ToolCategoryBuilder().WithIntegrationId(integrationId).Build();
        var toolAction = new ToolActionBuilder()
            .WithId(toolActionId)
            .WithToolCategoryId(toolCategory.Id)
            .AsMcpTool(integrationId)
            .WithIsEnabled(true)
            .Build();

        _toolActionDataAccess.GetByIdAsync(toolActionId, Arg.Any<CancellationToken>()).Returns(toolAction);
        _toolCategoryDataAccess.GetByIdAsync(toolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(toolCategory);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);

        var result = await _sut.ToggleToolActionEnabledAsync(userId, toolActionId, false);

        Assert.False(result.IsEnabled);
    }

    [Fact]
    public async Task ToggleToolActionEnabledAsync_ReturnsToolActionDetailDto()
    {
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var toolActionId = Guid.NewGuid();

        var integration = new IntegrationBuilder().WithId(integrationId).Build();
        var toolCategory = new ToolCategoryBuilder().WithIntegrationId(integrationId).Build();
        var toolAction = new ToolActionBuilder()
            .WithId(toolActionId)
            .WithToolCategoryId(toolCategory.Id)
            .WithName("use_figma")
            .WithDangerLevel(DangerLevel.Destructive)
            .AsMcpTool(integrationId, """{"type":"object"}""")
            .WithIsEnabled(false)
            .Build();

        _toolActionDataAccess.GetByIdAsync(toolActionId, Arg.Any<CancellationToken>()).Returns(toolAction);
        _toolCategoryDataAccess.GetByIdAsync(toolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(toolCategory);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);

        var result = await _sut.ToggleToolActionEnabledAsync(userId, toolActionId, true);

        Assert.IsType<ToolActionDetailDto>(result);
        Assert.Equal(toolActionId, result.Id);
        Assert.Equal("use_figma", result.Name);
        Assert.Equal("Destructive", result.DangerLevel);
        Assert.True(result.IsMcpTool);
        Assert.NotNull(result.McpToolSchema);
    }

    [Fact]
    public async Task ToggleToolActionEnabledAsync_WhenToolActionNotFound_ThrowsToolActionNotFoundException()
    {
        var userId = Guid.NewGuid();
        var toolActionId = Guid.NewGuid();
        _toolActionDataAccess.GetByIdAsync(toolActionId, Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        await Assert.ThrowsAsync<ToolActionNotFoundException>(
            () => _sut.ToggleToolActionEnabledAsync(userId, toolActionId, true));
    }

    [Fact]
    public async Task ToggleToolActionEnabledAsync_WhenUserLacksAccess_ThrowsUnauthorizedWorkspaceAccessException()
    {
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var toolActionId = Guid.NewGuid();

        var integration = new IntegrationBuilder().WithId(integrationId).Build();
        var toolCategory = new ToolCategoryBuilder().WithIntegrationId(integrationId).Build();
        var toolAction = new ToolActionBuilder()
            .WithId(toolActionId)
            .WithToolCategoryId(toolCategory.Id)
            .AsMcpTool(integrationId)
            .Build();

        _toolActionDataAccess.GetByIdAsync(toolActionId, Arg.Any<CancellationToken>()).Returns(toolAction);
        _toolCategoryDataAccess.GetByIdAsync(toolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(toolCategory);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, integration.WorkspaceId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(userId, integration.WorkspaceId));

        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => _sut.ToggleToolActionEnabledAsync(userId, toolActionId, true));
    }

    [Fact]
    public async Task GetAvailableToolsAsync_WithOnlyNativeIntegrations_DoesNotCallGetByIntegrationIds()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var nativeIntegration = new IntegrationBuilder()
            .WithWorkspaceId(workspaceId)
            .WithProvider(ProviderType.JIRA)
            .Build();

        var nativeCategory = new ToolCategoryBuilder()
            .WithProviderType(ProviderType.JIRA)
            .WithServiceClassName("JiraToolService")
            .Build();

        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _integrationDataAccess
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { nativeIntegration });
        _toolCategoryDataAccess
            .GetByProviderTypesAsync(Arg.Any<List<ProviderType>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolCategory> { nativeCategory });
        _toolActionDataAccess
            .GetByCategoryIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        var result = await _sut.GetAvailableToolsAsync(userId, workspaceId);

        Assert.Single(result);
        Assert.Equal("native", result[0].Source);
        await _toolCategoryDataAccess
            .DidNotReceive()
            .GetByIntegrationIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // FR-004: AssignToolActionsToAgentAsync — MCP Tool Assignment
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AssignToolActionsToAgentAsync_WithMixedMcpAndNativeToolActionIds_PersistsAllToAgentToolAction()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var nativeAction = new ToolActionBuilder()
            .WithIsEnabled(true)
            .Build();

        var mcpAction = new ToolActionBuilder()
            .AsMcpTool(integrationId)
            .WithIsEnabled(true)
            .Build();

        var toolActionIds = new List<Guid> { nativeAction.Id, mcpAction.Id };

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _toolActionDataAccess
            .GetEnabledByIdsAsync(toolActionIds, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction> { nativeAction, mcpAction });

        await _sut.AssignToolActionsToAgentAsync(userId, agentId, toolActionIds);

        await _agentToolActionDataAccess.Received(1).AssignToolActionsAsync(
            agentId,
            toolActionIds,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignToolActionsToAgentAsync_WithNonOptedInDestructiveMcpToolId_ThrowsToolActionNotFoundException()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var validNativeId = Guid.NewGuid();
        var nonOptedInDestructiveId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var validNativeAction = new ToolActionBuilder()
            .WithId(validNativeId)
            .WithIsEnabled(true)
            .Build();

        var toolActionIds = new List<Guid> { validNativeId, nonOptedInDestructiveId };

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _toolActionDataAccess
            .GetEnabledByIdsAsync(toolActionIds, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction> { validNativeAction });

        var exception = await Assert.ThrowsAsync<ToolActionNotFoundException>(
            () => _sut.AssignToolActionsToAgentAsync(userId, agentId, toolActionIds));

        Assert.Equal(nonOptedInDestructiveId, exception.ToolActionId);
        await _agentToolActionDataAccess
            .DidNotReceive()
            .AssignToolActionsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignToolActionsToAgentAsync_WithEmptyToolActionIdList_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.AssignToolActionsToAgentAsync(userId, agentId, new List<Guid>()));
    }

    [Fact]
    public async Task AssignToolActionsToAgentAsync_WhenAgentNotFound_ThrowsAgentNotFoundException()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns((Agent?)null);

        await Assert.ThrowsAsync<AgentNotFoundException>(
            () => _sut.AssignToolActionsToAgentAsync(userId, agentId, new List<Guid> { Guid.NewGuid() }));
    }

    [Fact]
    public async Task AssignToolActionsToAgentAsync_WhenUserLacksAccess_ThrowsUnauthorizedWorkspaceAccessException()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithWorkspaceId(workspaceId)
            .Build();

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(userId, workspaceId));

        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => _sut.AssignToolActionsToAgentAsync(userId, agentId, new List<Guid> { Guid.NewGuid() }));
    }

    // -----------------------------------------------------------------------
    // FR-004: RemoveToolActionsFromAgentAsync — MCP Tool Removal
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RemoveToolActionsFromAgentAsync_WithMcpToolActionIds_DeletesAgentToolActionRecords()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var mcpActionId1 = Guid.NewGuid();
        var mcpActionId2 = Guid.NewGuid();
        var toolActionIds = new List<Guid> { mcpActionId1, mcpActionId2 };

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _sut.RemoveToolActionsFromAgentAsync(userId, agentId, toolActionIds);

        await _agentToolActionDataAccess.Received(1).RemoveToolActionsAsync(
            agentId,
            toolActionIds,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveToolActionsFromAgentAsync_WhenAgentNotFound_ThrowsAgentNotFoundException()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns((Agent?)null);

        await Assert.ThrowsAsync<AgentNotFoundException>(
            () => _sut.RemoveToolActionsFromAgentAsync(userId, agentId, new List<Guid> { Guid.NewGuid() }));
    }

    // -----------------------------------------------------------------------
    // FR-004: GetAgentToolActionsAsync — MCP Badge Rendering
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolActionsAsync_WithMcpToolAction_MapsIsMcpToolTrueAndSourceMcp()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var mcpAction = new ToolActionBuilder()
            .AsMcpTool(integrationId, """{"type":"object"}""")
            .WithIsEnabled(true)
            .Build();

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpAction.Id });
        _toolActionDataAccess
            .GetByIdAsync(mcpAction.Id, Arg.Any<CancellationToken>())
            .Returns(mcpAction);

        var result = await _sut.GetAgentToolActionsAsync(userId, agentId);

        var action = Assert.Single(result);
        Assert.True(action.IsMcpTool);
        Assert.Equal("mcp", action.Source);
        Assert.NotNull(action.McpToolSchema);
    }

    [Fact]
    public async Task GetAgentToolActionsAsync_WithNativeToolAction_MapsIsMcpToolFalseAndSourceNative()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var nativeAction = new ToolActionBuilder()
            .WithIsEnabled(true)
            .Build();

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { nativeAction.Id });
        _toolActionDataAccess
            .GetByIdAsync(nativeAction.Id, Arg.Any<CancellationToken>())
            .Returns(nativeAction);

        var result = await _sut.GetAgentToolActionsAsync(userId, agentId);

        var action = Assert.Single(result);
        Assert.False(action.IsMcpTool);
        Assert.Equal("native", action.Source);
    }

    // ── Scenario 5: GET /v1/tools must not return MCP categories ─────────────

    [Fact]
    public async Task GetAvailableToolsAsync_WhenWorkspaceHasMcpIntegration_DoesNotReturnMcpCategories()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var mcpIntegrationId = Guid.NewGuid();

        var mcpIntegration = new IntegrationBuilder()
            .WithId(mcpIntegrationId)
            .WithIsMcpBacked(true)
            .AsConnected(true)
            .Build();

        var nativeCategory = new ToolCategoryBuilder()
            .WithProviderType(ProviderType.INTERNAL)
            .Build();

        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { mcpIntegration });
        _toolCategoryDataAccess.GetByProviderTypesAsync(Arg.Any<List<ProviderType>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolCategory> { nativeCategory });
        _toolActionDataAccess.GetByCategoryIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        var result = await _sut.GetAvailableToolsAsync(userId, workspaceId);

        Assert.DoesNotContain(result, dto => dto.IsMcpCategory);
        Assert.DoesNotContain(result, dto => dto.Source == "mcp");
    }

    [Fact]
    public async Task GetAvailableToolsAsync_WhenWorkspaceHasMcpIntegration_NeverCallsGetByIntegrationIdsAsync()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var mcpIntegrationId = Guid.NewGuid();

        var mcpIntegration = new IntegrationBuilder()
            .WithId(mcpIntegrationId)
            .WithIsMcpBacked(true)
            .AsConnected(true)
            .Build();

        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { mcpIntegration });
        _toolCategoryDataAccess.GetByProviderTypesAsync(Arg.Any<List<ProviderType>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolCategory>());
        _toolActionDataAccess.GetByCategoryIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        await _sut.GetAvailableToolsAsync(userId, workspaceId);

        await _toolCategoryDataAccess
            .DidNotReceive()
            .GetByIntegrationIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAvailableToolsAsync_WhenWorkspaceHasOnlyMcpIntegration_ReturnsEmptyList()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var mcpIntegration = new IntegrationBuilder()
            .WithIsMcpBacked(true)
            .AsConnected(true)
            .Build();

        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { mcpIntegration });
        _toolCategoryDataAccess.GetByProviderTypesAsync(Arg.Any<List<ProviderType>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolCategory>());
        _toolActionDataAccess.GetByCategoryIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        var result = await _sut.GetAvailableToolsAsync(userId, workspaceId);

        Assert.Empty(result);
    }
}
