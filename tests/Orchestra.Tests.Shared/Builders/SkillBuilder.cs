using Bogus;
using Orchestra.Domain.Entities;

namespace Orchestra.Tests.Shared.Builders;

/// <summary>
/// Fluent builder for creating Skill test entities with sensible defaults.
/// </summary>
public class SkillBuilder
{
    private static readonly Faker _faker = new();

    private Guid _workspaceId = Guid.NewGuid();
    private string _name = "test-skill";
    private string _description = _faker.Lorem.Sentence(8);
    private string _instructions = _faker.Lorem.Paragraph(2);

    /// <summary>
    /// Sets the workspace ID that owns this skill.
    /// </summary>
    public SkillBuilder WithWorkspaceId(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    /// <summary>
    /// Sets the skill name.
    /// </summary>
    public SkillBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the skill description.
    /// </summary>
    public SkillBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the skill instructions.
    /// </summary>
    public SkillBuilder WithInstructions(string instructions)
    {
        _instructions = instructions;
        return this;
    }

    /// <summary>
    /// Builds a <see cref="Skill"/> using the configured values.
    /// </summary>
    public Skill Build() =>
        Skill.Create(_workspaceId, _name, _description, _instructions);
}
