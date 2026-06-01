namespace Orchestra.Application.Skills.DTOs;

public record SkillFolderDto(
    string Id,
    string WorkspaceId,
    string Name,
    string FolderPath,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
