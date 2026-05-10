namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// Response shape for GET /v1/agents/{agentId}/mcp-tools.
/// Used by the Tool Picker to pre-populate existing selections when the modal re-opens.
/// </summary>
public record AgentToolAssignmentsDto(
    IReadOnlyList<Guid> NativeToolActionIds,
    IReadOnlyList<AgentMcpServerAssignmentDto> McpAssignments);

/// <summary>
/// Groups MCP tool names by their server. Each server appears at most once.
/// </summary>
public record AgentMcpServerAssignmentDto(
    Guid McpServerId,
    IReadOnlyList<string> ToolNames);
