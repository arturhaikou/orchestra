using System.Text.Json.Serialization;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for managing ticket pagination state and token generation/parsing.
/// Handles serialization of pagination cursor tokens for stateless API pagination.
/// </summary>
public interface ITicketPaginationService
{
    /// <summary>
    /// Parses a base64-encoded pagination token to extract state.
    /// Returns default token if token is null or invalid.
    /// </summary>
    /// <param name="pageToken">Base64-encoded pagination token</param>
    /// <returns>Parsed pagination state</returns>
    TicketPageToken ParsePageToken(string? pageToken);

    /// <summary>
    /// Serializes pagination state to a base64-encoded token.
    /// </summary>
    /// <param name="pageToken">Pagination state to serialize</param>
    /// <returns>Base64-encoded token or null</returns>
    string? SerializePageToken(TicketPageToken pageToken);

    /// <summary>
    /// Normalizes page size to enforce constraints (min 50, max 100).
    /// </summary>
    /// <param name="pageSize">Requested page size</param>
    /// <returns>Normalized page size</returns>
    int NormalizePageSize(int pageSize);
}

/// <summary>
/// Internal structure for pagination token.
/// Serialized to base64-encoded JSON for opaque cursor-based pagination.
/// Supports phased pagination: internal tickets first, then external tickets.
/// </summary>
public class TicketPageToken
{
    /// <summary>
    /// Current pagination phase: "internal" or "external"
    /// </summary>
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "internal";
    
    /// <summary>
    /// Offset for internal ticket pagination (0-based)
    /// </summary>
    [JsonPropertyName("internalOffset")]
    public int InternalOffset { get; set; } = 0;
    
    /// <summary>
    /// State for external ticket pagination across multiple providers
    /// </summary>
    [JsonPropertyName("externalState")]
    public ExternalPaginationState? ExternalState { get; set; }
}

/// <summary>
/// Tracks pagination state when fetching external tickets from multiple providers.
/// </summary>
public class ExternalPaginationState
{
    /// <summary>
    /// Index of the current provider being fetched (0-based)
    /// </summary>
    [JsonPropertyName("currentProviderIndex")]
    public int CurrentProviderIndex { get; set; } = 0;
    
    /// <summary>
    /// Provider-specific continuation tokens mapped by integration ID
    /// </summary>
    [JsonPropertyName("providerTokens")]
    public Dictionary<string, string?> ProviderTokens { get; set; } = new();
    
    /// <summary>
    /// Total number of external tickets fetched so far across all providers
    /// </summary>
    [JsonPropertyName("totalExternalFetched")]
    public int TotalExternalFetched { get; set; } = 0;
    
    /// <summary>
    /// List of integration IDs (as strings) that have been exhausted (returned 0 tickets).
    /// These providers are skipped in subsequent redistribution rounds.
    /// Persisted in the page token to avoid re-querying exhausted providers.
    /// </summary>
    [JsonPropertyName("exhaustedProviderIds")]
    public List<string> ExhaustedProviderIds { get; set; } = new();
}
