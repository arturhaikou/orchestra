namespace Orchestra.Domain.Exceptions;

public class McpDiscoveryTimeoutException : Exception
{
    public Guid IntegrationId { get; }
    public TimeSpan Timeout { get; }

    public McpDiscoveryTimeoutException(Guid integrationId, TimeSpan timeout)
        : base($"MCP tool discovery for integration '{integrationId}' timed out after {timeout.TotalSeconds:0}s.")
    {
        IntegrationId = integrationId;
        Timeout = timeout;
    }

    public McpDiscoveryTimeoutException(Guid integrationId, TimeSpan timeout, Exception innerException)
        : base($"MCP tool discovery for integration '{integrationId}' timed out after {timeout.TotalSeconds:0}s.", innerException)
    {
        IntegrationId = integrationId;
        Timeout = timeout;
    }
}
