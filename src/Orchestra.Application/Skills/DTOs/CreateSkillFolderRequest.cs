namespace Orchestra.Application.Skills.DTOs;

public record CreateSkillFolderRequest(
    Guid WorkspaceId,
    string Name,
    string FolderPath
);
