namespace Orchestra.Application.Common.Exceptions;

public class TemplateNotFoundException : Exception
{
    public string TemplateId { get; }

    public TemplateNotFoundException(string templateId)
        : base($"Template '{templateId}' not found.")
    {
        TemplateId = templateId;
    }
}
