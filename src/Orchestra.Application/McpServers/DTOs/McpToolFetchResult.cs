namespace Orchestra.Application.McpServers.DTOs;

public abstract record McpToolFetchResult
{
    private McpToolFetchResult() { }

    public sealed record Success(IReadOnlyList<McpToolItem> Tools) : McpToolFetchResult;

    public sealed record Empty() : McpToolFetchResult;

    public sealed record Unreachable(string Message) : McpToolFetchResult;

    public sealed record AuthFailed() : McpToolFetchResult;
}
