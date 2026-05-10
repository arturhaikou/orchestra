using Orchestra.Application.McpServers.DTOs;

namespace Orchestra.Application.McpServers.Interfaces;

/// <summary>
/// Performs a live tool fetch from a remote MCP server by its stored ID.
/// Returns a structured result — never throws to the caller.
/// </summary>
public interface IMcpServerToolFetcher
{
    /// <summary>
    /// Connects to the live MCP server identified by <paramref name="mcpServerId"/>,
    /// retrieves its current tool list, and classifies each tool's danger level.
    /// </summary>
    /// <param name="userId">Used to validate workspace membership.</param>
    /// <param name="workspaceId">Workspace scope; rejects cross-workspace access.</param>
    /// <param name="mcpServerId">The ID of the McpServer record to fetch tools from.</param>
    /// <param name="cancellationToken">Supports client-side request cancellation.</param>
    /// <returns>
    /// A <see cref="McpToolFetchResult"/>: Success, Empty, Unreachable, or AuthFailed.
    /// </returns>
    Task<McpToolFetchResult> FetchToolsAsync(
        Guid userId,
        Guid workspaceId,
        Guid mcpServerId,
        CancellationToken cancellationToken = default);
}
