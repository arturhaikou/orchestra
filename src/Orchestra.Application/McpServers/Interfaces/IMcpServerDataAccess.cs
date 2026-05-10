using Orchestra.Domain.Entities;

namespace Orchestra.Application.McpServers.Interfaces;

public interface IMcpServerDataAccess
{
    Task<bool> ExistsByNameAsync(
        Guid workspaceId,
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<McpServer?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<McpServer>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        McpServer server,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        McpServer server,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        McpServer server,
        CancellationToken cancellationToken = default);
}
