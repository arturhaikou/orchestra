using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Domain.Entities;

public class AiCliIntegration : IWorkspaceScopedEntity
{
    private AiCliIntegration() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AiCliProviderType Provider { get; private set; }
    public string? EncryptedCredential { get; private set; }
    public bool UseLoggedInUser { get; private set; }
    public string WorkingDirectory { get; private set; } = string.Empty;
    public string? CliPath { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static AiCliIntegration Create(
        Guid workspaceId,
        string name,
        AiCliProviderType provider,
        string? encryptedCredential,
        bool useLoggedInUser,
        string workingDirectory,
        string? cliPath = null)
    {
        ValidateName(name);
        ValidateWorkingDirectory(workingDirectory);
        ValidateCredential(encryptedCredential, useLoggedInUser);
        ValidateCliPath(cliPath);

        return new AiCliIntegration
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            Provider = provider,
            EncryptedCredential = encryptedCredential,
            UseLoggedInUser = useLoggedInUser,
            WorkingDirectory = workingDirectory.Trim(),
            CliPath = cliPath?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? encryptedCredential, bool useLoggedInUser, string workingDirectory, string? cliPath = null)
    {
        ValidateName(name);
        ValidateWorkingDirectory(workingDirectory);
        ValidateCredential(encryptedCredential, useLoggedInUser);
        ValidateCliPath(cliPath);

        Name = name.Trim();
        EncryptedCredential = encryptedCredential;
        UseLoggedInUser = useLoggedInUser;
        WorkingDirectory = workingDirectory.Trim();
        CliPath = cliPath?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be empty.", nameof(name));

        if (name.Trim().Length > 100)
            throw new ArgumentException("Name must not exceed 100 characters.", nameof(name));
    }

    private static void ValidateWorkingDirectory(string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
            throw new ArgumentException("Working directory must not be empty.", nameof(workingDirectory));

        if (workingDirectory.Trim().Length > 500)
            throw new ArgumentException("Working directory must not exceed 500 characters.", nameof(workingDirectory));
    }

    private static void ValidateCredential(string? encryptedCredential, bool useLoggedInUser)
    {
        if (!useLoggedInUser && string.IsNullOrWhiteSpace(encryptedCredential))
            throw new ArgumentException(
                "A credential must be provided when UseLoggedInUser is false.",
                nameof(encryptedCredential));
    }

    private static void ValidateCliPath(string? cliPath)
    {
        if (cliPath is not null && cliPath.Trim().Length > 500)
            throw new ArgumentException("CLI path must not exceed 500 characters.", nameof(cliPath));
    }
}
