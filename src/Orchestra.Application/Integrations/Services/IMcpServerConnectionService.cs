using Orchestra.Application.Integrations.DTOs;

namespace Orchestra.Application.Integrations.Services;

/// <summary>
/// Performs a stateless live-connection probe against an MCP server.
/// Discovers the tool catalogue and returns it without persisting anything.
/// </summary>
public interface IMcpServerConnectionService
{
    /// <summary>
    /// Connects to the specified MCP server and returns its tool list.
    /// </summary>
    /// <param name="userId">
    ///   The ID of the authenticated caller — used for workspace membership check.
    /// </param>
    /// <param name="request">
    ///   Transport type and credentials. Nothing in this object is persisted.
    /// </param>
    /// <param name="cancellationToken">
    ///   ASP.NET request cancellation token — propagated through the full call chain.
    /// </param>
    /// <returns>A <see cref="ConnectMcpServerResponseDto"/> containing the discovered tool list.</returns>
    /// <exception cref="Orchestra.Application.Common.Exceptions.UnauthorizedWorkspaceAccessException">
    ///   Thrown when the caller is not a member of the target workspace.
    /// </exception>
    /// <exception cref="Orchestra.Domain.Exceptions.McpConnectionException">
    ///   Thrown when the MCP server is unreachable, rejects authentication, or times out.
    /// </exception>
    /// <exception cref="Orchestra.Domain.Exceptions.ProcessLaunchException">
    ///   Thrown when the Stdio command cannot be spawned.
    /// </exception>
    Task<ConnectMcpServerResponseDto> ConnectAsync(
        Guid userId,
        ConnectMcpServerRequest request,
        CancellationToken cancellationToken = default);
}
