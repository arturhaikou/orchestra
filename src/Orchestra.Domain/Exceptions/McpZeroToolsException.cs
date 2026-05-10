namespace Orchestra.Domain.Exceptions;

public class McpZeroToolsException : Exception
{
    public McpZeroToolsException()
        : base("MCP server returned no tools") { }
}
