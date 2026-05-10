using NSubstitute;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tools.Services;
using Orchestra.Domain.Enums;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tools;

// FR-005: GetAgentToolActionsAsync — Transport Badge Fields
public class ToolServiceGetAgentToolActionsBadgeTests
{
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess = Substitute.For<IToolCategoryDataAccess>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly ToolService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Guid _agentId = Guid.NewGuid();

    public ToolServiceGetAgentToolActionsBadgeTests()
    {
        _sut = new ToolService(
            _toolCategoryDataAccess,
            _toolActionDataAccess,
            _agentToolActionDataAccess,
            _integrationDataAccess,
            _agentDataAccess,
            _workspaceAuthorizationService);
    }

    // -----------------------------------------------------------------------
    // Scenario 1 — Mixed native + MCP stdio assignment: badge fields populated
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolActionsAsync_WithStdioMcpToolAction_PopulatesTransportStdio()
    {
        var integrationId = Guid.NewGuid();
        const string integrationName = "My Filesystem MCP";

        var agent = new AgentBuilder()
            .WithId(_agentId)
            .WithWorkspaceId(_workspaceId)
            .Build();

        var mcpAction = new ToolActionBuilder()
            .AsMcpTool(integrationId)
            .WithIsEnabled(true)
            .Build();

        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithName(integrationName)
            .WithIsMcpBacked(true)
            .WithMcpTransportType(McpTransportType.STDIO)
            .AsConnected(true)
            .Build();

        _agentDataAccess.GetByIdAsync(_agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _integrationDataAccess
            .GetByWorkspaceIdAsync(_workspaceId, Arg.Any<CancellationToken>())
            .Returns([integration]);
        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(_agentId, Arg.Any<CancellationToken>())
            .Returns([mcpAction.Id]);
        _toolActionDataAccess
            .GetByIdAsync(mcpAction.Id, Arg.Any<CancellationToken>())
            .Returns(mcpAction);

        var result = await _sut.GetAgentToolActionsAsync(_userId, _agentId);

        var actionDto = Assert.Single(result);
        Assert.Equal("STDIO", actionDto.Transport);
    }

    [Fact]
    public async Task GetAgentToolActionsAsync_WithStdioMcpToolAction_PopulatesIntegrationName()
    {
        var integrationId = Guid.NewGuid();
        const string integrationName = "My Filesystem MCP";

        var agent = new AgentBuilder()
            .WithId(_agentId)
            .WithWorkspaceId(_workspaceId)
            .Build();

        var mcpAction = new ToolActionBuilder()
            .AsMcpTool(integrationId)
            .WithIsEnabled(true)
            .Build();

        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithName(integrationName)
            .WithIsMcpBacked(true)
            .WithMcpTransportType(McpTransportType.STDIO)
            .AsConnected(true)
            .Build();

        _agentDataAccess.GetByIdAsync(_agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _integrationDataAccess
            .GetByWorkspaceIdAsync(_workspaceId, Arg.Any<CancellationToken>())
            .Returns([integration]);
        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(_agentId, Arg.Any<CancellationToken>())
            .Returns([mcpAction.Id]);
        _toolActionDataAccess
            .GetByIdAsync(mcpAction.Id, Arg.Any<CancellationToken>())
            .Returns(mcpAction);

        var result = await _sut.GetAgentToolActionsAsync(_userId, _agentId);

        var actionDto = Assert.Single(result);
        Assert.Equal(integrationName, actionDto.IntegrationName);
    }

    [Fact]
    public async Task GetAgentToolActionsAsync_WithNativeToolAction_TransportIsNull()
    {
        var agent = new AgentBuilder()
            .WithId(_agentId)
            .WithWorkspaceId(_workspaceId)
            .Build();

        var nativeAction = new ToolActionBuilder()
            .WithIsEnabled(true)
            .Build();

        _agentDataAccess.GetByIdAsync(_agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _integrationDataAccess
            .GetByWorkspaceIdAsync(_workspaceId, Arg.Any<CancellationToken>())
            .Returns([]);
        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(_agentId, Arg.Any<CancellationToken>())
            .Returns([nativeAction.Id]);
        _toolActionDataAccess
            .GetByIdAsync(nativeAction.Id, Arg.Any<CancellationToken>())
            .Returns(nativeAction);

        var result = await _sut.GetAgentToolActionsAsync(_userId, _agentId);

        var actionDto = Assert.Single(result);
        Assert.Null(actionDto.Transport);
        Assert.Null(actionDto.IntegrationName);
    }

    [Fact]
    public async Task GetAgentToolActionsAsync_WithMixedNativeAndMcpActions_OnlyMcpActionsHaveTransportBadge()
    {
        var integrationId = Guid.NewGuid();
        const string integrationName = "Stdio FS Server";

        var agent = new AgentBuilder()
            .WithId(_agentId)
            .WithWorkspaceId(_workspaceId)
            .Build();

        var nativeAction = new ToolActionBuilder()
            .WithIsEnabled(true)
            .Build();

        var mcpAction = new ToolActionBuilder()
            .AsMcpTool(integrationId)
            .WithIsEnabled(true)
            .Build();

        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithName(integrationName)
            .WithIsMcpBacked(true)
            .WithMcpTransportType(McpTransportType.STDIO)
            .AsConnected(true)
            .Build();

        _agentDataAccess.GetByIdAsync(_agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _integrationDataAccess
            .GetByWorkspaceIdAsync(_workspaceId, Arg.Any<CancellationToken>())
            .Returns([integration]);
        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(_agentId, Arg.Any<CancellationToken>())
            .Returns([nativeAction.Id, mcpAction.Id]);
        _toolActionDataAccess
            .GetByIdAsync(nativeAction.Id, Arg.Any<CancellationToken>())
            .Returns(nativeAction);
        _toolActionDataAccess
            .GetByIdAsync(mcpAction.Id, Arg.Any<CancellationToken>())
            .Returns(mcpAction);

        var result = await _sut.GetAgentToolActionsAsync(_userId, _agentId);

        Assert.Equal(2, result.Count);
        var nativeDto = result.Single(a => !a.IsMcpTool);
        var mcpDto = result.Single(a => a.IsMcpTool);
        Assert.Null(nativeDto.Transport);
        Assert.Null(nativeDto.IntegrationName);
        Assert.Equal("STDIO", mcpDto.Transport);
        Assert.Equal(integrationName, mcpDto.IntegrationName);
    }

    [Fact]
    public async Task GetAgentToolActionsAsync_FetchesWorkspaceIntegrations_ToResolveMcpBadgeData()
    {
        var agent = new AgentBuilder()
            .WithId(_agentId)
            .WithWorkspaceId(_workspaceId)
            .Build();

        _agentDataAccess.GetByIdAsync(_agentId, Arg.Any<CancellationToken>())
            .Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _integrationDataAccess
            .GetByWorkspaceIdAsync(_workspaceId, Arg.Any<CancellationToken>())
            .Returns([]);
        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(_agentId, Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.GetAgentToolActionsAsync(_userId, _agentId);

        await _integrationDataAccess
            .Received(1)
            .GetByWorkspaceIdAsync(_workspaceId, Arg.Any<CancellationToken>());
    }
}
