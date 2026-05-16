namespace Orchestra.Application.Skills.DTOs;

/// <summary>
/// Request DTO for creating a new <see cref="Orchestra.Domain.Entities.Skill"/>.
/// </summary>
public record CreateSkillRequest(
    Guid WorkspaceId,
    string Name,
    string Description,
    string Instructions
);
