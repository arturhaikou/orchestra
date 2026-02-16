using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

public class Integration
{
    private Integration() { } // EF Core constructor

    // Constructor for creating Integration with decrypted API key (used by providers)
    public Integration(
        Guid id,
        Guid workspaceId,
        string name,
        ProviderType provider,
        string? url,
        string apiKey, // Decrypted API key
        string? filterQuery,
        IntegrationType integrationType,
        JiraType? jiraType = null)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        Provider = provider;
        Url = url;
        // Note: ApiKey is not stored, only used transiently
        FilterQuery = filterQuery;
        Type = integrationType;
        JiraType = jiraType;
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public IntegrationType Type { get; private set; }
    public string? Icon { get; private set; }
    public ProviderType Provider { get; private set; }
    public JiraType? JiraType { get; private set; }
    public string? Url { get; private set; }
    public string? Username { get; private set; }
    public string? EncryptedApiKey { get; private set; }
    public string? FilterQuery { get; private set; }
    public bool Vectorize { get; private set; }
    public bool Connected { get; private set; }
    public DateTime? LastSyncAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation property
    public Workspace Workspace { get; private set; } = null!;

    /// <summary>
    /// Factory method to create a new Integration with validation.
    /// </summary>
    public static Integration Create(
        Guid workspaceId,
        string name,
        IntegrationType type,
        ProviderType provider,
        string? url = null,
        string? username = null,
        string? encryptedApiKey = null,
        string? filterQuery = null,
        bool vectorize = false,
        JiraType? jiraType = null)
    {
        // Validate workspace ID
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("WorkspaceId cannot be empty.", nameof(workspaceId));
        
        // Validate and trim name
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length < 2 || trimmedName.Length > 100)
            throw new ArgumentException("Name must be between 2 and 100 characters.", nameof(name));
        
        // Validate URL format if provided
        if (!string.IsNullOrEmpty(url) && !Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("URL must be a valid absolute URL.", nameof(url));
        
        return new Integration
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = trimmedName,
            Type = type,
            Icon = provider.ToString().ToLowerInvariant(),
            Provider = provider,
            Url = url,
            Username = username,
            EncryptedApiKey = encryptedApiKey,
            FilterQuery = filterQuery,
            Vectorize = vectorize,
            JiraType = jiraType ?? Orchestra.Domain.Enums.JiraType.Cloud, // Default to Cloud
            Connected = true, // Default to connected
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    /// <summary>
    /// Updates the integration with new values, applying validation and conditional API key handling.
    /// The API key is only updated if a new value is provided; otherwise, the existing key is preserved.
    /// </summary>
    /// <param name="name">The new name for the integration (2-100 characters).</param>
    /// <param name="provider">The provider type (optional, only updated if specified).</param>
    /// <param name="url">The URL for the integration (optional, must be absolute if specified).</param>
    /// <param name="username">The username for authentication (optional).</param>
    /// <param name="encryptedApiKey">The encrypted API key (optional, only updated if provided).</param>
    /// <param name="jiraType">The Jira instance type (optional, only updated if specified).</param>
    /// <param name="connected">The connection status (optional, only updated if provided).</param>
    public void Update(
        string name,
        ProviderType? provider = null,
        string? url = null,
        string? username = null,
        string? encryptedApiKey = null,
        string? filterQuery = null,
        bool vectorize = false,
        JiraType? jiraType = null,
        bool? connected = null)
    {
        // Validate and trim name
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length < 2 || trimmedName.Length > 100)
            throw new ArgumentException("Name must be between 2 and 100 characters.", nameof(name));
        
        // Validate URL format if provided
        if (!string.IsNullOrEmpty(url) && !Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("URL must be a valid absolute URL.", nameof(url));
        
        // Update properties
        Name = trimmedName;
        if (provider.HasValue)
        {
            Icon = provider.Value.ToString().ToLowerInvariant();
            Provider = provider.Value;
        }
        Url = url;
        Username = username;
        FilterQuery = filterQuery;
        Vectorize = vectorize;
        if (jiraType.HasValue)
        {
            JiraType = jiraType;
        }
        FilterQuery = filterQuery;
        Vectorize = vectorize;
        UpdatedAt = DateTime.UtcNow;
        
        // Only update API key if a new value is provided
        if (!string.IsNullOrEmpty(encryptedApiKey))
        {
            EncryptedApiKey = encryptedApiKey;
        }

        // Only update connected status if provided
        if (connected.HasValue)
        {
            Connected = connected.Value;
        }
    }

    /// <summary>
    /// Deactivates the integration (soft delete).
    /// Sets IsActive and Connected to false, preserving audit trail.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        Connected = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
