using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.McpServers.Interfaces;

public interface IMcpServerConnectionChecker
{
    Task<McpConnectionStatus> CheckAsync(
        McpServer server,
        CancellationToken cancellationToken = default);
}
