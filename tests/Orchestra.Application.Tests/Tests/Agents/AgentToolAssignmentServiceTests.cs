using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="AgentToolAssignmentService"/>.
/// Covers all BDD scenarios from FR-005: save (Sc.1), get pre-populated (Sc.2),
/// replace (Sc.3), empty-delete (Sc.4), plus agent-not-found and unauthorized edge cases.
/// </summary>
public class AgentToolAssignmentServiceTests
{
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess;
    private readonly IAgentMcpToolDataAccess _agentMcpToolDataAccess;
    private readonly AgentToolAssignmentService _sut;

    public AgentToolAssignmentServiceTests()
    {
        _agentDataAccess = Substitute.For<IAgentDataAccess>();
        _workspaceAuthorizationService = Substitute.For<IWorkspaceAuthorizationService>();
        _agentToolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
        _agentMcpToolDataAccess = Substitute.For<IAgentMcpToolDataAccess>();

        _sut = new AgentToolAssignmentService(
            _agentDataAccess,
            _workspaceAuthorizationService,
            _agentToolActionDataAccess,
            _agentMcpToolDataAccess);
    }

    // ──── SaveAssignmentsAsync — Happy Path ─────────────────────────────────────

    [Fact]
    public async Task SaveAssignmentsAsync_WithMcpSelections_CallsReplaceForEachServer()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);

        var request = new SaveAgentToolsRequest(
            NativeToolActionIds: [],
            McpSelections: [new McpToolSelectionDto(serverId, ["tool1", "tool2", "tool3"])]);

        await _sut.SaveAssignmentsAsync(userId, agentId, request);

        await _agentMcpToolDataAccess.Received(1).ReplaceForAgentAndServerAsync(
            agentId,
            serverId,
            Arg.Is<IReadOnlyList<AgentMcpTool>>(list =>
                list.Count == 3 && list.All(x => x.AgentId == agentId && x.McpServerId == serverId)));
    }

    [Fact]
    public async Task SaveAssignmentsAsync_WithNativeAndMcpSelections_CallsBothDataAccessMethods()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var toolActionId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);

        var request = new SaveAgentToolsRequest(
            NativeToolActionIds: [toolActionId],
            McpSelections: [new McpToolSelectionDto(serverId, ["tool1"])]);

        await _sut.SaveAssignmentsAsync(userId, agentId, request);

        await _agentToolActionDataAccess.Received(1).RemoveAllToolActionsAsync(agentId);
        await _agentToolActionDataAccess.Received(1).AssignToolActionsAsync(
            agentId,
            Arg.Is<List<Guid>>(ids => ids.Contains(toolActionId)));
        await _agentMcpToolDataAccess.Received(1).ReplaceForAgentAndServerAsync(
            agentId,
            serverId,
            Arg.Any<IReadOnlyList<AgentMcpTool>>());
    }

    [Fact]
    public async Task SaveAssignmentsAsync_WithUpdatedMcpSelection_CallsReplaceWithNewToolNamesOnly()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);

        var request = new SaveAgentToolsRequest(
            NativeToolActionIds: [],
            McpSelections: [new McpToolSelectionDto(serverId, ["tool3"])]);

        await _sut.SaveAssignmentsAsync(userId, agentId, request);

        await _agentMcpToolDataAccess.Received(1).ReplaceForAgentAndServerAsync(
            agentId,
            serverId,
            Arg.Is<IReadOnlyList<AgentMcpTool>>(list =>
                list.Count == 1 && list[0].ToolName == "tool3"));
    }

    [Fact]
    public async Task SaveAssignmentsAsync_WithEmptyToolNames_CallsReplaceWithEmptyList()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);

        var request = new SaveAgentToolsRequest(
            NativeToolActionIds: [],
            McpSelections: [new McpToolSelectionDto(serverId, [])]);

        await _sut.SaveAssignmentsAsync(userId, agentId, request);

        await _agentMcpToolDataAccess.Received(1).ReplaceForAgentAndServerAsync(
            agentId,
            serverId,
            Arg.Is<IReadOnlyList<AgentMcpTool>>(list => list.Count == 0));
    }

    [Fact]
    public async Task SaveAssignmentsAsync_WithNoMcpSelections_DoesNotCallMcpDataAccess()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);

        var request = new SaveAgentToolsRequest(
            NativeToolActionIds: [],
            McpSelections: []);

        await _sut.SaveAssignmentsAsync(userId, agentId, request);

        await _agentMcpToolDataAccess.DidNotReceive().ReplaceForAgentAndServerAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<AgentMcpTool>>());
    }

    [Fact]
    public async Task SaveAssignmentsAsync_WithMultipleMcpServers_CallsReplaceForEachServer()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var serverId1 = Guid.NewGuid();
        var serverId2 = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);

        var request = new SaveAgentToolsRequest(
            NativeToolActionIds: [],
            McpSelections: [
                new McpToolSelectionDto(serverId1, ["tool1", "tool2"]),
                new McpToolSelectionDto(serverId2, ["tool3"])
            ]);

        await _sut.SaveAssignmentsAsync(userId, agentId, request);

        await _agentMcpToolDataAccess.Received(1).ReplaceForAgentAndServerAsync(
            agentId, serverId1, Arg.Any<IReadOnlyList<AgentMcpTool>>());
        await _agentMcpToolDataAccess.Received(1).ReplaceForAgentAndServerAsync(
            agentId, serverId2, Arg.Any<IReadOnlyList<AgentMcpTool>>());
        await _agentMcpToolDataAccess.Received(2).ReplaceForAgentAndServerAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<AgentMcpTool>>());
    }

    // ──── SaveAssignmentsAsync — Error Path ─────────────────────────────────────

    [Fact]
    public async Task SaveAssignmentsAsync_WithAgentNotFound_ThrowsAgentNotFoundException()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _agentDataAccess.GetByIdAsync(agentId).Returns((Agent?)null);

        var request = new SaveAgentToolsRequest(
            NativeToolActionIds: [],
            McpSelections: []);

        await Assert.ThrowsAsync<AgentNotFoundException>(
            () => _sut.SaveAssignmentsAsync(userId, agentId, request));
    }

    [Fact]
    public async Task SaveAssignmentsAsync_WithUnauthorizedUser_PropagatesUnauthorizedWorkspaceAccessException()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(userId, agent.WorkspaceId));

        var request = new SaveAgentToolsRequest(
            NativeToolActionIds: [],
            McpSelections: []);

        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => _sut.SaveAssignmentsAsync(userId, agentId, request));
    }

    // ──── GetAssignmentsAsync — Happy Path ──────────────────────────────────────

    [Fact]
    public async Task GetAssignmentsAsync_WithExistingAssignments_ReturnsGroupedByServer()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var toolId1 = Guid.NewGuid();
        var toolId2 = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);
        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId).Returns([toolId1, toolId2]);
        _agentMcpToolDataAccess.GetByAgentIdAsync(agentId).Returns([
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).WithToolName("tool1").Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).WithToolName("tool2").Build()
        ]);

        var result = await _sut.GetAssignmentsAsync(userId, agentId);

        Assert.Contains(toolId1, result.NativeToolActionIds);
        Assert.Contains(toolId2, result.NativeToolActionIds);
        Assert.Single(result.McpAssignments);
        Assert.Equal(serverId, result.McpAssignments[0].McpServerId);
        Assert.Contains("tool1", result.McpAssignments[0].ToolNames);
        Assert.Contains("tool2", result.McpAssignments[0].ToolNames);
    }

    [Fact]
    public async Task GetAssignmentsAsync_WithNoAssignments_ReturnsEmptyCollections()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);
        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId).Returns([]);
        _agentMcpToolDataAccess.GetByAgentIdAsync(agentId).Returns([]);

        var result = await _sut.GetAssignmentsAsync(userId, agentId);

        Assert.Empty(result.NativeToolActionIds);
        Assert.Empty(result.McpAssignments);
    }

    [Fact]
    public async Task GetAssignmentsAsync_WithMcpToolsFromMultipleServers_GroupsCorrectly()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var serverId1 = Guid.NewGuid();
        var serverId2 = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);
        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId).Returns([]);
        _agentMcpToolDataAccess.GetByAgentIdAsync(agentId).Returns([
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId1).WithToolName("tool1").Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId1).WithToolName("tool2").Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId2).WithToolName("tool3").Build()
        ]);

        var result = await _sut.GetAssignmentsAsync(userId, agentId);

        Assert.Equal(2, result.McpAssignments.Count);

        var server1Assignment = result.McpAssignments.Single(a => a.McpServerId == serverId1);
        Assert.Equal(2, server1Assignment.ToolNames.Count);
        Assert.Contains("tool1", server1Assignment.ToolNames);
        Assert.Contains("tool2", server1Assignment.ToolNames);

        var server2Assignment = result.McpAssignments.Single(a => a.McpServerId == serverId2);
        Assert.Single(server2Assignment.ToolNames);
        Assert.Contains("tool3", server2Assignment.ToolNames);
    }

    // ──── GetAssignmentsAsync — Error Path ──────────────────────────────────────

    [Fact]
    public async Task GetAssignmentsAsync_WithAgentNotFound_ThrowsAgentNotFoundException()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        _agentDataAccess.GetByIdAsync(agentId).Returns((Agent?)null);

        await Assert.ThrowsAsync<AgentNotFoundException>(
            () => _sut.GetAssignmentsAsync(userId, agentId));
    }

    [Fact]
    public async Task GetAssignmentsAsync_WithUnauthorizedUser_PropagatesUnauthorizedWorkspaceAccessException()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentDataAccess.GetByIdAsync(agentId).Returns(agent);
        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(userId, agent.WorkspaceId));

        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => _sut.GetAssignmentsAsync(userId, agentId));
    }
}
