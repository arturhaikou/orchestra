using Orchestra.Application.Common;

namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// Request DTO for updating an existing Agent.
/// Supports partial updates with nullable fields.
/// Model uses Optional&lt;string?&gt; to distinguish "field absent" (no change) from
/// "field explicitly null" (clear model override → Default) and "field set" (update model).
/// </summary>
public record UpdateAgentRequest(
    string? Name,
    string? Role,
    string[]? Capabilities,
    string[]? ToolActionIds,
    string? CustomInstructions,
    Optional<string?> Model = default
);