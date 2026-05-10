namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// Request body for PUT /v1/agents/{agentId}/tools.
/// Atomically replaces all native tool-action assignments and all MCP tool assignments for the agent.
/// </summary>
public record SaveAgentToolsRequest(
    IReadOnlyList<Guid> NativeToolActionIds,
    IReadOnlyList<McpToolSelectionDto> McpSelections);

/// <summary>
/// Declares which tool names from a specific MCP server are approved for the agent.
/// Sending an empty <see cref="ToolNames"/> list removes all assignments for that server.
/// </summary>
public record McpToolSelectionDto(
    Guid McpServerId,
    IReadOnlyList<string> ToolNames);
