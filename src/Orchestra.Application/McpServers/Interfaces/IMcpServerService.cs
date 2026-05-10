using Orchestra.Application.Common.Exceptions;

namespace Orchestra.Application.McpServers.Interfaces;

public interface IMcpServerService
{
    Task<bool> IsNameUniqueAsync(
        Guid requestingUserId,
        Guid workspaceId,
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken = default);
}
