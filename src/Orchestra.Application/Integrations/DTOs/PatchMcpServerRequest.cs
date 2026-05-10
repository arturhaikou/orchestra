namespace Orchestra.Application.Integrations.DTOs;

/// <summary>
/// Inbound request body for PATCH /v1/integrations/mcp-servers/:id.
/// Shape is identical to <see cref="SaveMcpServerRequest"/> (FR-007).
/// Null credential values are the "preserve existing" sentinel.
/// </summary>
public sealed record PatchMcpServerRequest(
    Guid WorkspaceId,

    /// <summary>Updated display name. Must be unique (case-insensitive), excluding self.</summary>
    string Name,

    /// <summary>"HTTP" or "STDIO". Transport type may change from the original.</summary>
    string TransportType,

    PatchHttpFields? Http,
    PatchStdioFields? Stdio
);

/// <summary>HTTP transport connection details for PATCH.</summary>
public sealed record PatchHttpFields(
    string Url,

    /// <summary>"NONE", "API_KEY", or "BEARER_TOKEN".</summary>
    string AuthType,

    /// <summary>
    /// Null semantics:
    ///   null    → user did not change the key; backend preserves EncryptedApiKey.
    ///   non-null → user entered a new value; backend encrypts and replaces.
    /// Ignored when AuthType is "NONE".
    /// </summary>
    string? ApiKey
);

/// <summary>Stdio transport connection details for PATCH.</summary>
public sealed record PatchStdioFields(
    string Command,
    string[]? Args,
    PatchEnvVar[]? EnvVars
);

/// <summary>
/// A single env var for PATCH.
/// Null semantics for <see cref="Value"/>:
///   null    → preserve the existing encrypted value for this key.
///   non-null → encrypt and replace with this new value.
/// </summary>
public sealed record PatchEnvVar(string Key, string? Value);
