using Orchestra.Application.Agents.Templates;

namespace Orchestra.Application.Common.Interfaces;

public interface IBuiltInAgentTemplateRegistry
{
    IReadOnlyList<BuiltInAgentTemplate> GetAll();

    BuiltInAgentTemplate? GetByIdentifier(string identifier);
}
