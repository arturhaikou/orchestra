namespace Orchestra.Application.Integrations.DTOs;

public sealed record SaveMcpServerRequest(
    Guid WorkspaceId,
    string Name,
    string TransportType,
    SaveHttpFields? Http,
    SaveStdioFields? Stdio
);

public sealed record SaveHttpFields(
    string Url,
    string AuthType,
    string? ApiKey
);

public sealed record SaveStdioFields(
    string Command,
    string[]? Args,
    SaveEnvVar[]? EnvVars
);

public sealed record SaveEnvVar(string Key, string? Value);
