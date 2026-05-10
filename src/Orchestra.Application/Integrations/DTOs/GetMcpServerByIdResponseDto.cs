namespace Orchestra.Application.Integrations.DTOs;

/// <summary>
/// Safe configuration DTO returned by GET /v1/integrations/mcp-servers/:id.
/// Sensitive credentials (API keys, env var values) are NEVER included.
/// Their presence is communicated through boolean sentinel fields.
/// </summary>
public sealed record GetMcpServerByIdResponseDto(
    string Id,
    string WorkspaceId,
    string Name,

    /// <summary>"HTTP" or "STDIO".</summary>
    string TransportType,

    /// <summary>"Connected" or "ConnectionFailed".</summary>
    string ConnectionStatus,

    // ── HTTP fields ──────────────────────────────────────────────────────────
    /// <summary>Populated when TransportType is "HTTP".</summary>
    string? EndpointUrl,

    /// <summary>"NONE", "API_KEY", or "BEARER_TOKEN". Populated when TransportType is "HTTP".</summary>
    string? AuthType,

    /// <summary>
    /// True when an encrypted API key / bearer token is stored.
    /// The raw value is never returned.
    /// </summary>
    bool HasApiKey,

    // ── Stdio fields ─────────────────────────────────────────────────────────
    /// <summary>Populated when TransportType is "STDIO".</summary>
    string? Command,

    /// <summary>Parsed argument list. Populated when TransportType is "STDIO".</summary>
    string[]? Args,

    /// <summary>
    /// Environment variable KEYS only — values are never returned to the client.
    /// Populated when TransportType is "STDIO" and at least one env var is stored.
    /// </summary>
    string[]? EnvVarKeys
);
