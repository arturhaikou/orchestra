using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

/// <summary>
/// Stores the AI provider credentials for a single workspace.
/// </summary>
/// <remarks>
/// <para>
/// <b>SECURITY:</b> <see cref="Endpoint"/> and <see cref="ApiKey"/> hold
/// <b>ciphertext values only</b>. Callers must pass pre-encrypted values to
/// <see cref="Create"/> and must decrypt retrieved values externally via
/// <c>IProviderCredentialEncryptionService</c> (defined in the Application layer).
/// Storing plaintext credentials in these fields is a misuse of this entity.
/// </para>
/// </remarks>
public class AIProviderConfiguration
{
    /// <summary>Unique identifier for this configuration record (PK).</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Foreign key to the owning <c>Workspace</c>.
    /// Enforced as unique: one configuration per workspace.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>The AI provider type selected for this workspace.</summary>
    public AIProviderType ProviderType { get; private set; }

    /// <summary>
    /// Provider URL (nullable, max 2048 characters). Semantics are provider-type-dependent:
    /// for <see cref="AIProviderType.AzureOpenAI"/> this is the encrypted resource endpoint URL;
    /// for <see cref="AIProviderType.Ollama"/> this is the plaintext Ollama server base URL.
    /// </summary>
    public string? Endpoint { get; private set; }

    /// <summary>
    /// Azure OpenAI API key (ciphertext, nullable, max 4096 characters).
    /// Required when <see cref="ProviderType"/> is <see cref="AIProviderType.AzureOpenAI"/>;
    /// unused for <see cref="AIProviderType.Ollama"/>.
    /// </summary>
    public string? ApiKey { get; private set; }

    /// <summary>
    /// The default model identifier for this provider configuration (nullable, max 500 characters).
    /// Required and non-empty when <see cref="ProviderType"/> is <see cref="AIProviderType.Ollama"/>;
    /// optional (may be null) for <see cref="AIProviderType.AzureOpenAI"/>.
    /// </summary>
    public string? DefaultModelId { get; private set; }

    /// <summary>UTC timestamp when this configuration record was created.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>UTC timestamp of the last update; null if never updated.</summary>
    public DateTime? UpdatedAt { get; private set; }

    private AIProviderConfiguration() { } // For EF Core

    /// <summary>
    /// Creates a new <see cref="AIProviderConfiguration"/> record.
    /// </summary>
    /// <param name="workspaceId">The workspace this configuration belongs to.</param>
    /// <param name="providerType">The selected AI provider.</param>
    /// <param name="endpoint">
    /// For Azure OpenAI: encrypted endpoint URL (ciphertext — never plaintext).
    /// For Ollama: plaintext Ollama server base URL.
    /// </param>
    /// <param name="apiKey">
    /// Encrypted Azure OpenAI API key (ciphertext — never plaintext).
    /// Pass <see langword="null"/> for Ollama.
    /// </param>
    /// <param name="defaultModelId">The default model identifier for this configuration.</param>
    public static AIProviderConfiguration Create(
        Guid workspaceId,
        AIProviderType providerType,
        string? endpoint = null,
        string? apiKey = null,
        string? defaultModelId = null)
    {
        return new AIProviderConfiguration
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ProviderType = providerType,
            Endpoint = endpoint,
            ApiKey = apiKey,
            DefaultModelId = defaultModelId,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates this configuration record with new provider settings, re-encrypted credentials,
    /// and the default model identifier. Passing <see langword="null"/> for
    /// <paramref name="defaultModelId"/> explicitly clears any previously stored model ID —
    /// use this when switching to a provider where no model ID applies.
    /// </summary>
    /// <param name="providerType">The new AI provider type.</param>
    /// <param name="endpoint">
    /// For Azure OpenAI: encrypted endpoint URL (ciphertext — never plaintext).
    /// For Ollama: plaintext Ollama server base URL.
    /// Pass <see langword="null"/> to clear.
    /// </param>
    /// <param name="apiKey">
    /// Encrypted Azure OpenAI API key (ciphertext — never plaintext).
    /// Pass <see langword="null"/> for Ollama.
    /// </param>
    /// <param name="defaultModelId">
    /// The default model identifier for this provider configuration.
    /// Pass <see langword="null"/> to explicitly clear a stale value (e.g., when switching providers).
    /// </param>
    public void Update(
        AIProviderType providerType,
        string? endpoint,
        string? apiKey,
        string? defaultModelId)
    {
        ProviderType = providerType;
        Endpoint = endpoint;
        ApiKey = apiKey;
        DefaultModelId = defaultModelId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the default model identifier for this provider configuration record.
    /// Called during provider reconfiguration after the supplied model ID has been
    /// validated against the live provider's model list.
    /// </summary>
    /// <param name="defaultModelId">The confirmed model identifier to set as default.</param>
    public void UpdateDefaultModelId(string defaultModelId)
    {
        DefaultModelId = defaultModelId;
        UpdatedAt = DateTime.UtcNow;
    }
}
