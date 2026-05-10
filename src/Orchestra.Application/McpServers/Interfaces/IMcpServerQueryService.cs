using Orchestra.Application.Integrations.DTOs;

namespace Orchestra.Application.McpServers.Interfaces;

public interface IMcpServerQueryService
{
    Task<List<McpServerListItemDto>> GetListAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<GetMcpServerByIdResponseDto> GetByIdAsync(
        Guid userId,
        Guid workspaceId,
        Guid serverId,
        CancellationToken cancellationToken = default);
}
