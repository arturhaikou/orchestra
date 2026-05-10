namespace Orchestra.Application.Integrations.DTOs;

public sealed record ConnectMcpServerRequest(
    Guid WorkspaceId,
    string TransportType,
    ConnectHttpFields? Http,
    ConnectStdioFields? Stdio
);

public sealed record ConnectHttpFields(
    string Url,
    string AuthType,
    string? ApiKey
);

public sealed record ConnectStdioFields(
    string Command,
    string[]? Args,
    ConnectEnvVar[]? EnvVars
);

public sealed record ConnectEnvVar(string Key, string Value);
