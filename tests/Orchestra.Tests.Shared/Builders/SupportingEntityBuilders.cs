using Bogus;
using Orchestra.Domain.Enums;

namespace Orchestra.Tests.Shared.Builders;

/// <summary>
/// Fluent builder for creating ToolAction test entities with sensible defaults.
/// </summary>
public class ToolActionBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _toolCategoryId = Guid.NewGuid();
    private string _name = new Faker().Lorem.Word();
    private string? _description = new Faker().Lorem.Sentence();
    private string _methodName = new Faker().Lorem.Word() + "Async";
    private DangerLevel _dangerLevel = DangerLevel.Safe;

    /// <summary>
    /// Sets the tool action ID.
    /// </summary>
    public ToolActionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the tool category ID.
    /// </summary>
    public ToolActionBuilder WithToolCategoryId(Guid toolCategoryId)
    {
        _toolCategoryId = toolCategoryId;
        return this;
    }

    /// <summary>
    /// Sets the tool action name.
    /// </summary>
    public ToolActionBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the description.
    /// </summary>
    public ToolActionBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the method name.
    /// </summary>
    public ToolActionBuilder WithMethodName(string methodName)
    {
        _methodName = methodName;
        return this;
    }

    /// <summary>
    /// Sets the danger level.
    /// </summary>
    public ToolActionBuilder WithDangerLevel(DangerLevel dangerLevel)
    {
        _dangerLevel = dangerLevel;
        return this;
    }

    /// <summary>
    /// Builds the ToolAction entity.
    /// </summary>
    public ToolAction Build()
    {
        return ToolAction.Create(
            _toolCategoryId,
            _name,
            _description,
            _methodName,
            _dangerLevel);
    }

    /// <summary>
    /// Creates a safe tool action.
    /// </summary>
    public static ToolAction SafeToolAction()
    {
        return new ToolActionBuilder()
            .WithDangerLevel(DangerLevel.Safe)
            .Build();
    }

    /// <summary>
    /// Creates a moderate danger tool action.
    /// </summary>
    public static ToolAction ModerateToolAction()
    {
        return new ToolActionBuilder()
            .WithDangerLevel(DangerLevel.Moderate)
            .Build();
    }

    /// <summary>
    /// Creates a destructive tool action.
    /// </summary>
    public static ToolAction DestructiveToolAction()
    {
        return new ToolActionBuilder()
            .WithDangerLevel(DangerLevel.Destructive)
            .Build();
    }
}

/// <summary>
/// Fluent builder for creating TicketComment test entities with sensible defaults.
/// </summary>
public class TicketCommentBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _ticketId = Guid.NewGuid();
    private string _author = new Faker().Name.FullName();
    private string _content = new Faker().Lorem.Paragraph();

    /// <summary>
    /// Sets the comment ID.
    /// </summary>
    public TicketCommentBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the ticket ID.
    /// </summary>
    public TicketCommentBuilder WithTicketId(Guid ticketId)
    {
        _ticketId = ticketId;
        return this;
    }

    /// <summary>
    /// Sets the author name.
    /// </summary>
    public TicketCommentBuilder WithAuthor(string author)
    {
        _author = author;
        return this;
    }

    /// <summary>
    /// Sets the comment content.
    /// </summary>
    public TicketCommentBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Builds the TicketComment entity.
    /// </summary>
    public TicketComment Build()
    {
        return TicketComment.Create(_ticketId, _author, _content);
    }
}

/// <summary>
/// Fluent builder for creating AgentToolAction test entities with sensible defaults.
/// </summary>
public class AgentToolActionBuilder
{
    private Guid _agentId = Guid.NewGuid();
    private Guid _toolActionId = Guid.NewGuid();

    /// <summary>
    /// Sets the agent ID.
    /// </summary>
    public AgentToolActionBuilder WithAgentId(Guid agentId)
    {
        _agentId = agentId;
        return this;
    }

    /// <summary>
    /// Sets the tool action ID.
    /// </summary>
    public AgentToolActionBuilder WithToolActionId(Guid toolActionId)
    {
        _toolActionId = toolActionId;
        return this;
    }

    /// <summary>
    /// Builds the AgentToolAction entity.
    /// </summary>
    public AgentToolAction Build()
    {
        return AgentToolAction.Create(_agentId, _toolActionId);
    }
}
