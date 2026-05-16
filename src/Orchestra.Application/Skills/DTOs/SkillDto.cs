namespace Orchestra.Application.Skills.DTOs;

/// <summary>
/// Response DTO for the <see cref="Orchestra.Domain.Entities.Skill"/> entity.
/// </summary>
public record SkillDto(
    string Id,
    string WorkspaceId,
    string Name,
    string Description,
    string Instructions,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
