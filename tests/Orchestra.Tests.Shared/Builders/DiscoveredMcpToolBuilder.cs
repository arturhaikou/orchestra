using Bogus;
using Orchestra.Domain.Enums;
using Orchestra.Domain.ValueObjects;

namespace Orchestra.Tests.Shared.Builders;

/// <summary>
/// Fluent builder for creating DiscoveredMcpTool value objects in tests.
/// </summary>
public class DiscoveredMcpToolBuilder
{
    private string _name = new Faker().Hacker.Verb().Replace(" ", "_");
    private string? _description = new Faker().Lorem.Sentence();
    private DangerLevel _dangerLevel = DangerLevel.Safe;
    private string? _inputSchemaJson = null;
    private bool _enabled = true;

    public DiscoveredMcpToolBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public DiscoveredMcpToolBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public DiscoveredMcpToolBuilder WithDangerLevel(DangerLevel level)
    {
        _dangerLevel = level;
        return this;
    }

    public DiscoveredMcpToolBuilder WithInputSchemaJson(string? json)
    {
        _inputSchemaJson = json;
        return this;
    }

    public DiscoveredMcpToolBuilder AsEnabled(bool enabled)
    {
        _enabled = enabled;
        return this;
    }

    public DiscoveredMcpTool Build() =>
        new(_name, _description, _dangerLevel, _inputSchemaJson, _enabled);

    public static DiscoveredMcpTool SafeTool(string name = "get_file_content") =>
        new DiscoveredMcpToolBuilder().WithName(name).WithDangerLevel(DangerLevel.Safe).Build();

    public static DiscoveredMcpTool ModerateTool(string name = "update_component") =>
        new DiscoveredMcpToolBuilder().WithName(name).WithDangerLevel(DangerLevel.Moderate).Build();

    public static DiscoveredMcpTool DestructiveTool(string name = "delete_component") =>
        new DiscoveredMcpToolBuilder().WithName(name).WithDangerLevel(DangerLevel.Destructive).AsEnabled(false).Build();
}
