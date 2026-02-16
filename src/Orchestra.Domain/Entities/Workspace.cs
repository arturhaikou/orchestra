namespace Orchestra.Domain.Entities;

public class Workspace
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    private Workspace() { } // For EF Core

    public static Workspace Create(string name, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length < 2 || trimmedName.Length > 100)
        {
            throw new ArgumentException("Name must be between 2 and 100 characters.", nameof(name));
        }

        return new Workspace
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void UpdateName(string newName)
    {
        var trimmedName = newName?.Trim() ?? string.Empty;
        
        if (trimmedName.Length < 2 || trimmedName.Length > 100)
        {
            throw new ArgumentException(
                "Workspace name must be between 2 and 100 characters.", 
                nameof(newName));
        }
        
        Name = trimmedName;
        UpdatedAt = DateTime.UtcNow;
    }
}