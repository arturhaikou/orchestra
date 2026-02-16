namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// Request DTO for updating an existing Agent.
/// Supports partial updates with nullable fields.
/// </summary>
public record UpdateAgentRequest(
    string? Name,
    string? Role,
    string[]? Capabilities,
    string[]? ToolActionIds,
    string? CustomInstructions
);