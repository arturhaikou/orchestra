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
    /// Null when the agent is configured with a review tool (ProjectPrinciples is used instead).
    /// </summary>
    public string? CustomInstructions { get; private set; }

    /// <summary>
    /// Gets the project principles for code review agents.
    /// Null when the agent is not configured with a review tool (CustomInstructions is used instead).
    /// At most one of CustomInstructions and ProjectPrinciples is non-null for any agent.
    /// </summary>
    public string? ProjectPrinciples { get; private set; }

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
    /// Gets the optional LLM model identifier the agent is configured to use.
    /// A null value means no model override — the execution pipeline falls back to the system default.
    /// </summary>
    public string? Model { get; private set; }

    /// <summary>
    /// Gets the slug identifier of the built-in template this agent was created from.
    /// Null for custom (non-template) agents. Immutable after creation.
    /// </summary>
    public string? TemplateIdentifier { get; private set; }

    /// <summary>
    /// Gets the template version at the time of agent creation.
    /// Null for custom (non-template) agents. Immutable after creation.
    /// </summary>
    public int? TemplateVersion { get; private set; }

    /// <summary>
    /// Gets the identifier of the AI CLI integration bound to this agent at deployment time.
    /// Non-null only for CLI-based built-in agents (e.g. Agentic Search).
    /// Cleared automatically when the referenced integration is deleted (SetNull FK).
    /// </summary>
    public Guid? AiCliIntegrationId { get; private set; }

    /// <summary>
    /// Private constructor to enforce factory method usage.
    /// </summary>
    private Agent() { }

    /// <summary>
    /// Creates a new agent instance with validated parameters.
    /// Exactly one of <paramref name="customInstructions"/> or <paramref name="projectPrinciples"/>
    /// must be non-null; mutual exclusivity is enforced by the Application service layer before
    /// this factory is called. The factory validates field-level constraints only.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="name">The agent name (required, max 200 characters).</param>
    /// <param name="role">The agent role (required, max 200 characters).</param>
    /// <param name="capabilities">The agent capabilities (optional).</param>
    /// <param name="customInstructions">The custom instructions (nullable; max 5000 characters).</param>
    /// <param name="projectPrinciples">The project principles for review agents (nullable; max 5000 characters).</param>
    /// <param name="model">Optional LLM model override.</param>
    /// <returns>A new Agent instance.</returns>
    /// <exception cref="ArgumentException">Thrown when field-level validation fails.</exception>
    public static Agent Create(
        Guid workspaceId,
        string name,
        string role,
        IEnumerable<string>? capabilities,
        string? customInstructions,
        string? projectPrinciples = null,
        string? model = null,
        string? templateIdentifier = null,
        int? templateVersion = null)
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

        if (customInstructions != null)
        {
            if (customInstructions.Trim().Length == 0)
                throw new ArgumentException("CustomInstructions cannot be whitespace.", nameof(customInstructions));
        }

        if (projectPrinciples != null)
        {
            if (projectPrinciples.Trim().Length == 0)
                throw new ArgumentException("ProjectPrinciples cannot be whitespace.", nameof(projectPrinciples));
        }

        if ((templateIdentifier is null) != (templateVersion is null))
            throw new ArgumentException("TemplateIdentifier and TemplateVersion must both be null or both be non-null.");

        if (templateIdentifier != null && templateIdentifier.Length > 200)
            throw new ArgumentException("TemplateIdentifier cannot exceed 200 characters.", nameof(templateIdentifier));

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            Role = role.Trim(),
            Status = AgentStatus.Offline,
            CustomInstructions = customInstructions?.Trim(),
            ProjectPrinciples = projectPrinciples?.Trim(),
            Capabilities = capabilities?.ToList() ?? new List<string>(),
            CreatedAt = DateTime.UtcNow,
            Model = model,
            TemplateIdentifier = templateIdentifier,
            TemplateVersion = templateVersion
        };

        agent.AvatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={agent.Id}";

        return agent;
    }

    /// <summary>
    /// Updates the agent's profile information.
    /// Exactly one of <paramref name="customInstructions"/> or <paramref name="projectPrinciples"/>
    /// must be non-null; mutual exclusivity is enforced by the Application service layer.
    /// </summary>
    /// <param name="name">The new name (required, max 200 characters).</param>
    /// <param name="role">The new role (required, max 200 characters).</param>
    /// <param name="capabilities">The new capabilities.</param>
    /// <param name="customInstructions">The new custom instructions (nullable; max 5000 characters).</param>
    /// <param name="projectPrinciples">The new project principles for review agents (nullable; max 5000 characters).</param>
    /// <exception cref="ArgumentException">Thrown when field-level validation fails.</exception>
    public void UpdateProfile(
        string name,
        string role,
        IEnumerable<string>? capabilities,
        string? customInstructions,
        string? projectPrinciples = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters.", nameof(name));

        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or empty.", nameof(role));

        if (role.Length > 200)
            throw new ArgumentException("Role cannot exceed 200 characters.", nameof(role));

        if (customInstructions != null)
        {
            if (customInstructions.Trim().Length == 0)
                throw new ArgumentException("CustomInstructions cannot be whitespace.", nameof(customInstructions));
            if (customInstructions.Length > 5000)
                throw new ArgumentException("CustomInstructions cannot exceed 5000 characters.", nameof(customInstructions));
        }

        if (projectPrinciples != null)
        {
            if (projectPrinciples.Trim().Length == 0)
                throw new ArgumentException("ProjectPrinciples cannot be whitespace.", nameof(projectPrinciples));
            if (projectPrinciples.Length > 5000)
                throw new ArgumentException("ProjectPrinciples cannot exceed 5000 characters.", nameof(projectPrinciples));
        }

        Name = name.Trim();
        Role = role.Trim();
        Capabilities = capabilities?.ToList() ?? new List<string>();
        CustomInstructions = customInstructions?.Trim();
        ProjectPrinciples = projectPrinciples?.Trim();
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

    /// <summary>
    /// Sets (or clears) the LLM model override for this agent.
    /// Pass null to clear the override and revert to the system default.
    /// </summary>
    /// <param name="model">The model identifier, or null to clear.</param>
    public void SetModel(string? model)
    {
        Model = model;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Binds or clears the AI CLI integration associated with this agent.
    /// </summary>
    /// <param name="integrationId">The CLI integration identifier, or null to clear.</param>
    public void SetAiCliIntegrationId(Guid? integrationId)
    {
        AiCliIntegrationId = integrationId;
        UpdatedAt = DateTime.UtcNow;
    }
}