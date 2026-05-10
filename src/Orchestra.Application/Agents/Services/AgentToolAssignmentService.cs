using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Application.Agents.Services;

public class AgentToolAssignmentService : IAgentToolAssignmentService
{
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess;
    private readonly IAgentMcpToolDataAccess _agentMcpToolDataAccess;

    public AgentToolAssignmentService(
        IAgentDataAccess agentDataAccess,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IAgentToolActionDataAccess agentToolActionDataAccess,
        IAgentMcpToolDataAccess agentMcpToolDataAccess)
    {
        _agentDataAccess = agentDataAccess
            ?? throw new ArgumentNullException(nameof(agentDataAccess));
        _workspaceAuthorizationService = workspaceAuthorizationService
            ?? throw new ArgumentNullException(nameof(workspaceAuthorizationService));
        _agentToolActionDataAccess = agentToolActionDataAccess
            ?? throw new ArgumentNullException(nameof(agentToolActionDataAccess));
        _agentMcpToolDataAccess = agentMcpToolDataAccess
            ?? throw new ArgumentNullException(nameof(agentMcpToolDataAccess));
    }

    public async Task SaveAssignmentsAsync(
        Guid userId,
        Guid agentId,
        SaveAgentToolsRequest request,
        CancellationToken cancellationToken = default)
    {
        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken)
            ?? throw new AgentNotFoundException(agentId);

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, agent.WorkspaceId, cancellationToken);

        await _agentToolActionDataAccess.RemoveAllToolActionsAsync(agentId, cancellationToken);

        if (request.NativeToolActionIds.Count > 0)
            await _agentToolActionDataAccess.AssignToolActionsAsync(agentId, request.NativeToolActionIds.ToList(), cancellationToken);

        foreach (var selection in request.McpSelections)
        {
            var tools = selection.ToolNames
                .Select(name => AgentMcpTool.Create(agentId, selection.McpServerId, name))
                .ToList();

            await _agentMcpToolDataAccess.ReplaceForAgentAndServerAsync(agentId, selection.McpServerId, tools, cancellationToken);
        }
    }

    public async Task<AgentToolAssignmentsDto> GetAssignmentsAsync(
        Guid userId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken)
            ?? throw new AgentNotFoundException(agentId);

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, agent.WorkspaceId, cancellationToken);

        var nativeIds = await _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, cancellationToken);
        var mcpTools = await _agentMcpToolDataAccess.GetByAgentIdAsync(agentId, cancellationToken);

        var mcpGroups = mcpTools
            .GroupBy(x => x.McpServerId)
            .Select(g => new AgentMcpServerAssignmentDto(g.Key, g.Select(x => x.ToolName).ToList()))
            .ToList();

        return new AgentToolAssignmentsDto(nativeIds, mcpGroups);
    }
}
