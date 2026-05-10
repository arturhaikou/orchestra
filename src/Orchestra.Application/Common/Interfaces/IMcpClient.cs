namespace Orchestra.Application.Common.Interfaces;

public interface IMcpClient
{
    Task<IEnumerable<IMcpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken = default);
}
