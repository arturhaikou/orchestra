using Bogus;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Tests.Shared.Builders;

/// <summary>
/// Fluent builder for creating <see cref="McpServer"/> test entities with sensible defaults.
/// Defaults to HTTP transport with a random HTTPS endpoint.
/// </summary>
public sealed class McpServerBuilder
{
    private static readonly Faker Faker = new();

    private Guid? _id = null;
    private Guid _workspaceId = Guid.NewGuid();
    private string _name = Faker.Commerce.ProductName();
    private McpTransportType _transportType = McpTransportType.HTTP;

    // HTTP fields
    private string _endpointUrl = $"https://{Faker.Internet.DomainName()}/mcp";
    private McpAuthType _authType = McpAuthType.NONE;
    private string? _encryptedApiKey = null;

    // STDIO fields
    private string _command = "npx";
    private string? _arguments = "-y @modelcontextprotocol/server-filesystem /tmp";
    private string? _encryptedEnvironmentVariables = null;

    /// <summary>
    /// Sets the server ID. Useful when tests need to assert against a specific server ID.
    /// Uses reflection to set the private property on the built entity.
    /// </summary>
    public McpServerBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the workspace ID for this MCP server.
    /// </summary>
    public McpServerBuilder WithWorkspaceId(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    /// <summary>
    /// Sets the display name of this MCP server.
    /// </summary>
    public McpServerBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Forces STDIO transport. Also call <see cref="WithCommand"/> to set the command.
    /// </summary>
    public McpServerBuilder WithTransportType(McpTransportType transportType)
    {
        _transportType = transportType;
        return this;
    }

    /// <summary>
    /// Sets the HTTPS endpoint URL. Implicitly selects HTTP transport.
    /// </summary>
    public McpServerBuilder WithEndpointUrl(string url)
    {
        _endpointUrl = url;
        _transportType = McpTransportType.HTTP;
        return this;
    }

    /// <summary>
    /// Sets the authentication type for the HTTP MCP server.
    /// </summary>
    public McpServerBuilder WithAuthType(McpAuthType authType)
    {
        _authType = authType;
        return this;
    }

    /// <summary>
    /// Sets the encrypted API key for API_KEY authenticated HTTP servers.
    /// </summary>
    public McpServerBuilder WithEncryptedApiKey(string? encryptedApiKey)
    {
        _encryptedApiKey = encryptedApiKey;
        return this;
    }

    /// <summary>
    /// Sets the command for a STDIO MCP server. Implicitly selects STDIO transport.
    /// </summary>
    public McpServerBuilder WithCommand(string command)
    {
        _command = command;
        _transportType = McpTransportType.STDIO;
        return this;
    }

    /// <summary>
    /// Sets the command-line arguments for the STDIO process.
    /// </summary>
    public McpServerBuilder WithArguments(string? arguments)
    {
        _arguments = arguments;
        return this;
    }

    /// <summary>
    /// Sets the encrypted environment variables for the STDIO process.
    /// </summary>
    public McpServerBuilder WithEncryptedEnvironmentVariables(string? encryptedEnvironmentVariables)
    {
        _encryptedEnvironmentVariables = encryptedEnvironmentVariables;
        return this;
    }

    /// <summary>
    /// Builds the <see cref="McpServer"/> entity using the appropriate factory method
    /// based on the configured transport type.
    /// </summary>
    public McpServer Build()
    {
        var server = _transportType == McpTransportType.HTTP
            ? McpServer.CreateHttp(_workspaceId, _name, _endpointUrl, _authType, _encryptedApiKey)
            : McpServer.CreateStdio(_workspaceId, _name, _command, _arguments, _encryptedEnvironmentVariables);

        if (_id.HasValue)
        {
            typeof(McpServer).GetProperty(nameof(McpServer.Id))!.SetValue(server, _id.Value);
        }

        return server;
    }
}
