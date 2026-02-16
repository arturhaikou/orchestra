using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

/// <summary>
/// Represents an AI agent within a workspace, encapsulating its configuration,
/// capabilities, and runtime state.
/// </summary>
public class Agent
{
    /// <summary>
    /// Gets the unique identifier of the agent.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the workspace identifier this agent belongs to.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Gets the display name of the agent.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the role or persona of the agent.
    /// </summary>
    public string Role { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the current runtime status of the agent.
    /// </summary>
    public AgentStatus Status { get; private set; }

    /// <summary>
    /// Gets the URL for the agent's avatar image.
    /// </summary>
    public string AvatarUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the custom instructions for the agent.
    /// </summary>
    public string CustomInstructions { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the collection of capability tags for the agent.
    /// </summary>
    public ICollection<string> Capabilities { get; private set; } = new List<string>();

    /// <summary>
    /// Gets the creation timestamp of the agent.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets the last modification timestamp of the agent.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Private constructor to enforce factory method usage.
    /// </summary>
    private Agent() { }

    /// <summary>
    /// Creates a new agent instance with validated parameters.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="name">The agent name (required, max 200 characters).</param>
    /// <param name="role">The agent role (required, max 200 characters).</param>
    /// <param name="capabilities">The agent capabilities (optional).</param>
    /// <param name="customInstructions">The custom instructions (required, max 5000 characters).</param>
    /// <returns>A new Agent instance.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static Agent Create(
        Guid workspaceId,
        string name,
        string role,
        IEnumerable<string>? capabilities,
        string customInstructions)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("WorkspaceId cannot be empty.", nameof(workspaceId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters.", nameof(name));

        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or empty.", nameof(role));

        if (role.Length > 200)
            throw new ArgumentException("Role cannot exceed 200 characters.", nameof(role));

        if (string.IsNullOrWhiteSpace(customInstructions))
            throw new ArgumentException("CustomInstructions cannot be null or empty.", nameof(customInstructions));

        if (customInstructions.Length > 5000)
            throw new ArgumentException("CustomInstructions cannot exceed 5000 characters.", nameof(customInstructions));

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            Role = role.Trim(),
            Status = AgentStatus.Offline,
            CustomInstructions = customInstructions.Trim(),
            Capabilities = capabilities?.ToList() ?? new List<string>(),
            CreatedAt = DateTime.UtcNow
        };

        agent.AvatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={agent.Id}";

        return agent;
    }

    /// <summary>
    /// Updates the agent's profile information.
    /// </summary>
    /// <param name="name">The new name (required, max 200 characters).</param>
    /// <param name="role">The new role (required, max 200 characters).</param>
    /// <param name="capabilities">The new capabilities.</param>
    /// <param name="customInstructions">The new custom instructions (required, max 5000 characters).</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public void UpdateProfile(
        string name,
        string role,
        IEnumerable<string>? capabilities,
        string customInstructions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters.", nameof(name));

        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or empty.", nameof(role));

        if (role.Length > 200)
            throw new ArgumentException("Role cannot exceed 200 characters.", nameof(role));

        if (string.IsNullOrWhiteSpace(customInstructions))
            throw new ArgumentException("CustomInstructions cannot be null or empty.", nameof(customInstructions));

        if (customInstructions.Length > 5000)
            throw new ArgumentException("CustomInstructions cannot exceed 5000 characters.", nameof(customInstructions));

        Name = name.Trim();
        Role = role.Trim();
        Capabilities = capabilities?.ToList() ?? new List<string>();
        CustomInstructions = customInstructions.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the agent's runtime status.
    /// </summary>
    /// <param name="status">The new status.</param>
    public void UpdateStatus(AgentStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}