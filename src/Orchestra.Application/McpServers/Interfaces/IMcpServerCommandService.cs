using Orchestra.Application.Integrations.DTOs;

namespace Orchestra.Application.McpServers.Interfaces;

public interface IMcpServerCommandService
{
    Task<McpServerListItemDto> CreateAsync(
        Guid userId,
        SaveMcpServerRequest request,
        CancellationToken cancellationToken = default);

    Task<McpServerListItemDto> UpdateAsync(
        Guid userId,
        Guid serverId,
        PatchMcpServerRequest request,
        CancellationToken cancellationToken = default);

    Task<DeleteMcpServerResponseDto> DeleteAsync(
        Guid userId,
        Guid serverId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
