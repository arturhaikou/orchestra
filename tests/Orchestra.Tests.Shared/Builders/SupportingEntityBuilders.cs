using Bogus;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using System.Reflection;

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
    private string _methodName = new Faker().Lorem.Word() + "_action";
    private DangerLevel _dangerLevel = DangerLevel.Safe;
    private bool _isMcpTool = false;
    private Guid? _integrationId = null;
    private string? _mcpToolSchema = null;
    private bool _isEnabled = true;
    private bool _isActive = true;
    private DateTimeOffset? _lastSyncedAt = null;

    public ToolActionBuilder WithId(Guid id) { _id = id; return this; }
    public ToolActionBuilder WithToolCategoryId(Guid toolCategoryId) { _toolCategoryId = toolCategoryId; return this; }
    public ToolActionBuilder WithName(string name) { _name = name; return this; }
    public ToolActionBuilder WithDescription(string? description) { _description = description; return this; }
    public ToolActionBuilder WithMethodName(string methodName) { _methodName = methodName; return this; }
    public ToolActionBuilder WithDangerLevel(DangerLevel dangerLevel) { _dangerLevel = dangerLevel; return this; }
    public ToolActionBuilder WithIntegrationId(Guid integrationId)
    {
        _integrationId = integrationId;
        _isMcpTool = true;
        return this;
    }
    public ToolActionBuilder AsMcpTool(Guid integrationId, string? schema = null)
    {
        _isMcpTool = true;
        _integrationId = integrationId;
        _mcpToolSchema = schema ?? """{"type":"object","properties":{}}""";
        return this;
    }
    public ToolActionBuilder AsOrphanedMcpTool()
    {
        _isMcpTool = true;
        _integrationId = null;
        _mcpToolSchema = """{"type":"object","properties":{}}""";
        return this;
    }
    public ToolActionBuilder WithIsEnabled(bool isEnabled) { _isEnabled = isEnabled; return this; }
    public ToolActionBuilder AsActive(bool isActive = true) { _isActive = isActive; return this; }
    public ToolActionBuilder AsDeactivated() { _isActive = false; return this; }
    public ToolActionBuilder AsInactive() { _isActive = false; return this; }
    public ToolActionBuilder WithLastSyncedAt(DateTimeOffset syncedAt) { _lastSyncedAt = syncedAt; return this; }

    public ToolAction Build()
    {
        ToolAction toolAction;
        if (_isMcpTool && _integrationId.HasValue)
        {
            toolAction = ToolAction.CreateMcpTool(
                _toolCategoryId,
                _integrationId.Value,
                _name,
                _description,
                _methodName,
                _dangerLevel,
                _mcpToolSchema,
                _isEnabled);
        }
        else if (_isMcpTool)
        {
            toolAction = ToolAction.CreateMcpTool(
                _toolCategoryId,
                Guid.NewGuid(),
                _name,
                _description,
                _methodName,
                _dangerLevel,
                _mcpToolSchema,
                _isEnabled);
            typeof(ToolAction).GetProperty(nameof(ToolAction.IntegrationId))!
                .SetValue(toolAction, (Guid?)null);
        }
        else
        {
            toolAction = ToolAction.Create(_toolCategoryId, _name, _description, _methodName, _dangerLevel);
        }

        typeof(ToolAction).GetProperty(nameof(ToolAction.Id))!.SetValue(toolAction, _id);
        typeof(ToolAction).GetProperty(nameof(ToolAction.IsActive), BindingFlags.Public | BindingFlags.Instance)?.SetValue(toolAction, _isActive);
        typeof(ToolAction).GetProperty(nameof(ToolAction.LastSyncedAt), BindingFlags.Public | BindingFlags.Instance)?.SetValue(toolAction, _lastSyncedAt);
        return toolAction;
    }

    public static ToolAction SafeToolAction() =>
        new ToolActionBuilder().WithDangerLevel(DangerLevel.Safe).Build();

    public static ToolAction ModerateToolAction() =>
        new ToolActionBuilder().WithDangerLevel(DangerLevel.Moderate).Build();

    public static ToolAction DestructiveToolAction() =>
        new ToolActionBuilder().WithDangerLevel(DangerLevel.Destructive).Build();

    public static ToolAction SafeMcpToolAction(Guid integrationId) =>
        new ToolActionBuilder()
            .WithDangerLevel(DangerLevel.Safe)
            .AsMcpTool(integrationId)
            .WithIsEnabled(true)
            .Build();

    public static ToolAction DestructiveMcpToolAction(Guid integrationId) =>
        new ToolActionBuilder()
            .WithDangerLevel(DangerLevel.Destructive)
            .AsMcpTool(integrationId)
            .WithIsEnabled(false)
            .Build();
}

public class ToolCategoryBuilder
{
    private string _name = new Faker().Company.CompanyName();
    private string _description = new Faker().Lorem.Sentence();
    private ProviderType _providerType = ProviderType.MCP_GENERIC;
    private string _serviceClassName = new Faker().Lorem.Word() + "Service";
    private Guid? _integrationId = null;
    private bool _isActive = true;

    public ToolCategoryBuilder WithName(string name) { _name = name; return this; }
    public ToolCategoryBuilder WithDescription(string description) { _description = description; return this; }
    public ToolCategoryBuilder WithProviderType(ProviderType providerType) { _providerType = providerType; return this; }
    public ToolCategoryBuilder WithServiceClassName(string serviceClassName) { _serviceClassName = serviceClassName; return this; }
    public ToolCategoryBuilder WithIntegrationId(Guid integrationId) { _integrationId = integrationId; return this; }
    public ToolCategoryBuilder WithIsActive(bool isActive) { _isActive = isActive; return this; }
    public ToolCategoryBuilder AsDeactivated() { _isActive = false; return this; }

    public ToolCategory Build()
    {
        var category = _integrationId.HasValue
            ? ToolCategory.CreateMcpCategory(_name, _description, _providerType, _integrationId.Value)
            : ToolCategory.Create(_name, _description, _providerType, _serviceClassName);

        typeof(ToolCategory)
            .GetProperty(nameof(ToolCategory.IsActive), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .SetValue(category, _isActive);

        return category;
    }

    public static ToolCategory NativeCategory() =>
        new ToolCategoryBuilder()
            .WithProviderType(ProviderType.INTERNAL)
            .WithServiceClassName("InternalToolService")
            .Build();

    public static ToolCategory McpCategory(Guid integrationId) =>
        new ToolCategoryBuilder()
            .WithIntegrationId(integrationId)
            .Build();

    public static ToolCategory DeactivatedMcpCategory(Guid integrationId) =>
        new ToolCategoryBuilder()
            .WithIntegrationId(integrationId)
            .AsDeactivated()
            .Build();
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
