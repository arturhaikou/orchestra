namespace Orchestra.Domain.Entities;

public class SkillFolder
{
    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string FolderPath { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    private SkillFolder() { }

    public static SkillFolder Create(Guid workspaceId, string name, string folderPath)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID cannot be empty.", nameof(workspaceId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Skill folder name cannot be empty.", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Skill folder name cannot exceed 200 characters.", nameof(name));

        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("Folder path cannot be empty.", nameof(folderPath));

        return new SkillFolder
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            FolderPath = Path.GetFullPath(folderPath.Trim()),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string folderPath)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Skill folder name cannot be empty.", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Skill folder name cannot exceed 200 characters.", nameof(name));

        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("Folder path cannot be empty.", nameof(folderPath));

        Name = name.Trim();
        FolderPath = Path.GetFullPath(folderPath.Trim());
        UpdatedAt = DateTime.UtcNow;
    }
}
