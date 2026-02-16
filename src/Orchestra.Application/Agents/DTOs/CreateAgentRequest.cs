namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// Request DTO for creating a new Agent.
/// </summary>
public record CreateAgentRequest(
    Guid WorkspaceId,
    string Name,
    string Role,
    string[] Capabilities,
    string[]? ToolActionIds,
    string CustomInstructions
);