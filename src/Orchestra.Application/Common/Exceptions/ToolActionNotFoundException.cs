namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when a tool action is not found.
/// </summary>
public class ToolActionNotFoundException : Exception
{
    public Guid ToolActionId { get; }

    public ToolActionNotFoundException(Guid toolActionId)
        : base($"Tool action with ID '{toolActionId}' was not found.")
    {
        ToolActionId = toolActionId;
    }
}