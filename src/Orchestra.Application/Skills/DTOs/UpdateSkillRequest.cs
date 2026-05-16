namespace Orchestra.Application.Skills.DTOs;

/// <summary>
/// Request DTO for updating an existing <see cref="Orchestra.Domain.Entities.Skill"/>.
/// All fields are required; partial updates are not supported to keep skill content consistent.
/// </summary>
public record UpdateSkillRequest(
    string Name,
    string Description,
    string Instructions
);
