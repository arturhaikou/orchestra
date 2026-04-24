using Orchestra.Application.Agents.DTOs;

namespace Orchestra.Application.Common.Interfaces;

public interface ITemplateRegistry
{
    IReadOnlyList<TemplateDefinition> GetAllTemplates();

    TemplateDefinition? GetTemplate(string templateId);
}
