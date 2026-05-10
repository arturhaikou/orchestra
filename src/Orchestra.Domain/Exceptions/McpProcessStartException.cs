namespace Orchestra.Domain.Exceptions;

public class McpProcessStartException : Exception
{
    public string Command { get; }

    public McpProcessStartException(string command, string reason)
        : base($"Failed to start MCP process '{command}': {reason}")
    {
        Command = command;
    }

    public McpProcessStartException(string command, string reason, Exception innerException)
        : base($"Failed to start MCP process '{command}': {reason}", innerException)
    {
        Command = command;
    }
}
