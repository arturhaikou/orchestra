using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

public class McpServer
{
    private McpServer() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public McpTransportType TransportType { get; private set; }

    public string? EndpointUrl { get; private set; }
    public McpAuthType? AuthType { get; private set; }
    public string? EncryptedApiKey { get; private set; }

    public string? Command { get; private set; }
    public string? Arguments { get; private set; }
    public string? EncryptedEnvironmentVariables { get; private set; }

    public McpConnectionStatus ConnectionStatus { get; private set; } = McpConnectionStatus.Unknown;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static McpServer CreateHttp(
        Guid workspaceId,
        string name,
        string endpointUrl,
        McpAuthType authType,
        string? encryptedApiKey)
    {
        ValidateName(name);
        ValidateHttpsUrl(endpointUrl);

        return new McpServer
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            TransportType = McpTransportType.HTTP,
            EndpointUrl = endpointUrl,
            AuthType = authType,
            EncryptedApiKey = encryptedApiKey,
            ConnectionStatus = McpConnectionStatus.Unknown,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static McpServer CreateStdio(
        Guid workspaceId,
        string name,
        string command,
        string? arguments,
        string? encryptedEnvironmentVariables)
    {
        ValidateName(name);
        ValidateCommand(command);

        return new McpServer
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            TransportType = McpTransportType.STDIO,
            Command = command,
            Arguments = arguments,
            EncryptedEnvironmentVariables = encryptedEnvironmentVariables,
            ConnectionStatus = McpConnectionStatus.Unknown,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        McpTransportType transportType,
        string? endpointUrl,
        McpAuthType? authType,
        string? encryptedApiKey,
        string? command,
        string? arguments,
        string? encryptedEnvironmentVariables)
    {
        ValidateName(name);

        if (transportType == McpTransportType.HTTP && endpointUrl is not null)
            ValidateHttpsUrl(endpointUrl);

        if (transportType == McpTransportType.STDIO && command is not null)
            ValidateCommand(command);

        Name = name.Trim();
        TransportType = transportType;
        EndpointUrl = endpointUrl;
        AuthType = authType;
        EncryptedApiKey = encryptedApiKey;
        Command = command;
        Arguments = arguments;
        EncryptedEnvironmentVariables = encryptedEnvironmentVariables;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetConnectionStatus(McpConnectionStatus status) =>
        ConnectionStatus = status;

    private static void ValidateName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length < 2 || trimmed.Length > 100)
            throw new ArgumentException("Name must be between 2 and 100 characters.", nameof(name));
    }

    private static void ValidateHttpsUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new ArgumentException("MCP endpoint must use HTTPS.", nameof(url));
    }

    private static void ValidateCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command is required.", nameof(command));

        if (command.IndexOfAny(['&', '|', ';', '>', '$']) >= 0)
            throw new ArgumentException(
                "Command must not contain shell operators (&, |, ;, >, $).", nameof(command));
    }
}
