using Bogus;
using Orchestra.Domain.Enums;

namespace Orchestra.Tests.Shared.Builders;

/// <summary>
/// Fluent builder for creating Agent test entities with sensible defaults.
/// </summary>
public class AgentBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _workspaceId = Guid.NewGuid();
    private string _name = new Faker().Name.FirstName();
    private string _role = new Faker().Lorem.Word();
    private AgentStatus _status = AgentStatus.Idle;
    private string _customInstructions = new Faker().Lorem.Sentence(10);
    private List<string> _capabilities = new() { "code_execution", "document_analysis" };
    private string? _model = null;
    private string? _projectPrinciples = null;
    private string? _templateIdentifier = null;
    private int? _templateVersion = null;

    /// <summary>
    /// Sets the agent ID.
    /// </summary>
    public AgentBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the workspace ID.
    /// </summary>
    public AgentBuilder WithWorkspaceId(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    /// <summary>
    /// Sets the agent name.
    /// </summary>
    public AgentBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the agent role.
    /// </summary>
    public AgentBuilder WithRole(string role)
    {
        _role = role;
        return this;
    }

    /// <summary>
    /// Sets the agent status.
    /// </summary>
    public AgentBuilder WithStatus(AgentStatus status)
    {
        _status = status;
        return this;
    }

    /// <summary>
    /// Sets the custom instructions.
    /// </summary>
    public AgentBuilder WithCustomInstructions(string instructions)
    {
        _customInstructions = instructions;
        return this;
    }

    /// <summary>
    /// Sets the capabilities list.
    /// </summary>
    public AgentBuilder WithCapabilities(params string[] capabilities)
    {
        _capabilities = capabilities.ToList();
        return this;
    }

    /// <summary>
    /// Sets the LLM model override for the agent. Pass null (default) for no override.
    /// </summary>
    public AgentBuilder WithModel(string? model)
    {
        _model = model;
        return this;
    }

    /// <summary>
    /// Sets the project principles for a review-configured agent.
    /// Pass null (default) for non-review agents.
    /// </summary>
    public AgentBuilder WithProjectPrinciples(string? projectPrinciples)
    {
        _projectPrinciples = projectPrinciples;
        return this;
    }

    /// <summary>
    /// Sets the template identifier for a built-in agent.
    /// </summary>
    public AgentBuilder WithTemplateIdentifier(string? templateIdentifier)
    {
        _templateIdentifier = templateIdentifier;
        return this;
    }

    /// <summary>
    /// Sets the template version for a built-in agent.
    /// </summary>
    public AgentBuilder WithTemplateVersion(int? templateVersion)
    {
        _templateVersion = templateVersion;
        return this;
    }

    /// <summary>
    /// Builds the Agent entity.
    /// </summary>
    public Agent Build()
    {
        return Agent.Create(
            workspaceId: _workspaceId,
            name: _name,
            role: _role,
            capabilities: _capabilities,
            customInstructions: _projectPrinciples == null ? _customInstructions : null,
            projectPrinciples: _projectPrinciples,
            model: _model,
            templateIdentifier: _templateIdentifier,
            templateVersion: _templateVersion);
    }

    /// <summary>
    /// Creates an active agent with typical configuration.
    /// </summary>
    public static Agent ActiveAgent()
    {
        return new AgentBuilder()
            .WithStatus(AgentStatus.Idle)
            .Build();
    }

    /// <summary>
    /// Creates an offline agent.
    /// </summary>
    public static Agent OfflineAgent()
    {
        return new AgentBuilder()
            .WithStatus(AgentStatus.Offline)
            .Build();
    }

    /// <summary>
    /// Creates a busy agent (executing a task).
    /// </summary>
    public static Agent BusyAgent()
    {
        return new AgentBuilder()
            .WithStatus(AgentStatus.Busy)
            .Build();
    }
}
