namespace Orchestra.Application.Common.Exceptions;

public class TemplateAlreadyDeployedException : Exception
{
    public string TemplateId { get; }

    public TemplateAlreadyDeployedException(string templateId)
        : base($"Template '{templateId}' is already deployed in this workspace.")
    {
        TemplateId = templateId;
    }
}
