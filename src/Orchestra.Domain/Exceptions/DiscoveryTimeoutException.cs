namespace Orchestra.Domain.Exceptions;

public class DiscoveryTimeoutException : Exception
{
    public DiscoveryTimeoutException()
        : base("The MCP server did not respond within 30 seconds.") { }

    public DiscoveryTimeoutException(Exception innerException)
        : base("The MCP server did not respond within 30 seconds.", innerException) { }
}
