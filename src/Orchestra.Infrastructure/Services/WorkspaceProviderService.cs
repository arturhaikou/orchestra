using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using System.Text.Json;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Concrete implementation of <see cref="IWorkspaceProviderService"/>.
/// Orchestrates AI provider configuration create and update operations:
/// validates provider-type field requirements, encrypts credentials via
/// <see cref="IProviderCredentialEncryptionService"/>, and stages changes
/// through <see cref="IWorkspaceAIProviderRepository"/> without committing —
/// the caller holds the Unit of Work and must call <c>SaveChangesAsync</c>.
/// </summary>
public sealed class WorkspaceProviderService : IWorkspaceProviderService
{
    private readonly IWorkspaceAIProviderRepository _repository;
    private readonly IProviderCredentialEncryptionService _encryptionService;
    private readonly IAzureOpenAIModelDiscoveryService _azureDiscoveryService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWorkspaceDataAccess _workspaceDataAccess;

    public WorkspaceProviderService(
        IWorkspaceAIProviderRepository repository,
        IProviderCredentialEncryptionService encryptionService,
        IAzureOpenAIModelDiscoveryService azureDiscoveryService,
        IHttpClientFactory httpClientFactory,
        IWorkspaceDataAccess workspaceDataAccess)
    {
        _repository = repository;
        _encryptionService = encryptionService;
        _azureDiscoveryService = azureDiscoveryService;
        _httpClientFactory = httpClientFactory;
        _workspaceDataAccess = workspaceDataAccess;
    }

    /// <inheritdoc/>
    public async Task<Guid> CreateProviderConfigAsync(
        Guid workspaceId,
        AIProviderType providerType,
        string? endpoint,
        string? apiKey,
        string? defaultModelId,
        CancellationToken cancellationToken)
    {
        // Rule 1: Fail fast on duplicate — uniqueness check before any encryption or construction.
        var existing = await _repository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"An AI provider configuration already exists for workspace {workspaceId}.");
        }

        // Rule 2: Secondary defaultModelId validation for Ollama.
        // Primary validation is in WorkspaceService.ValidateProviderConfigFields.
        if (providerType == AIProviderType.Ollama && string.IsNullOrWhiteSpace(defaultModelId))
        {
            throw new ArgumentException(
                "defaultModelId is required when ProviderType is Ollama.",
                nameof(defaultModelId));
        }

        // Rule 3 & 4: Provider-type field validation. Validation must complete before any write.
        var (encryptedEndpoint, encryptedApiKey) =
            ValidateAndEncrypt(providerType, endpoint, apiKey);

        // Rule 5: Construct entity with encrypted/resolved fields and UTC creation timestamp.
        var config = AIProviderConfiguration.Create(
            workspaceId,
            providerType,
            encryptedEndpoint,
            encryptedApiKey,
            defaultModelId);

        // Rule 6: Stage the entity — do NOT call SaveChangesAsync.
        await _repository.AddAsync(config, cancellationToken);

        // Rule 7: Return the new entity's Id so the caller can link it to the workspace.
        return config.Id;
    }

    /// <inheritdoc/>
    public async Task UpdateProviderConfigAsync(
        Guid workspaceId,
        AIProviderType providerType,
        string? endpoint,
        string? apiKey,
        string? defaultModelId,
        CancellationToken cancellationToken)
    {
        // Rule 1: Load existing entity — throw if none found.
        var config = await _repository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        if (config is null)
        {
            throw new InvalidOperationException(
                $"No AI provider configuration found for workspace {workspaceId}.");
        }

        // Rule 2 & 3: Apply the same provider-type field validation as the create path.
        var (encryptedEndpoint, encryptedApiKey) =
            ValidateAndEncrypt(providerType, endpoint, apiKey);

        // Rule 4: Mutate the loaded entity with re-encrypted fields, updated timestamp,
        //         and the explicit defaultModelId (null clears a stale value from a prior provider).
        config.Update(providerType, encryptedEndpoint, encryptedApiKey, defaultModelId);

        // Rule 5: Stage the mutation — do NOT call SaveChangesAsync.
        await _repository.UpdateAsync(config, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        // Step 1: Load configuration — throw a domain-level error if absent.
        var config = await _repository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        if (config is null)
        {
            throw new InvalidOperationException(
                $"Workspace {workspaceId} has no AI provider configured.");
        }

        // Step 2: Dispatch to the correct provider path.
        return config.ProviderType switch
        {
            AIProviderType.AzureOpenAI => await GetAzureOpenAIModelsAsync(config, cancellationToken),
            AIProviderType.Ollama      => await GetOllamaModelsAsync(config, cancellationToken),
            _ => throw new InvalidOperationException(
                     $"Unsupported provider type: {config.ProviderType}.")
        };
    }

    /// <inheritdoc/>
    public async Task<ProviderValidationResult?> ValidateProviderAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        // Step 1: Load configuration — return null to signal "no configuration" so
        //         the caller (controller) can map it to 404 Not Found.
        var config = await _repository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        if (config is null)
        {
            return null;
        }

        var providerType = config.ProviderType.ToString();

        // Step 2: Attempt live connectivity probe via the relevant private helper.
        //         Catch AIProviderCommunicationException to convert a provider failure
        //         into a validation-failure result rather than propagating an exception.
        //         The endpoint must still return 200 OK — the probe itself succeeded.
        try
        {
            IReadOnlyList<string> models = config.ProviderType switch
            {
                AIProviderType.AzureOpenAI => await GetAzureOpenAIModelsAsync(config, cancellationToken),
                AIProviderType.Ollama      => await GetOllamaModelsAsync(config, cancellationToken),
                _ => throw new InvalidOperationException(
                         $"Unsupported provider type: {config.ProviderType}.")
            };

            // Step 3a: Connectivity succeeded — isValid true, empty errorMessage.
            // OllamaBaseUrl is populated for Ollama workspaces so the edit page can
            // pre-fill the endpoint input without an additional network call.
            return new ProviderValidationResult(
                true,
                providerType,
                models,
                null,
                config.ProviderType == AIProviderType.Ollama ? config.Endpoint : null);
        }
        catch (AIProviderCommunicationException ex)
        {
            // Step 3b: Known provider connectivity failure.
            // ex.Message is already sanitised per AIProviderCommunicationException's security
            // contract — it contains no credential values, URLs, or raw provider payloads.
            return new ProviderValidationResult(
                false,
                providerType,
                Array.Empty<string>(),
                ex.Message,
                config.ProviderType == AIProviderType.Ollama ? config.Endpoint : null);
        }
        catch (Exception)
        {
            // Step 3c: Unexpected failure (e.g., unsupported provider type branch).
            // Use a generic user-presentable message — never forward ex.Message.
            return new ProviderValidationResult(
                false,
                providerType,
                Array.Empty<string>(),
                "The provider could not be reached. Verify your workspace provider configuration.",
                config.ProviderType == AIProviderType.Ollama ? config.Endpoint : null);
        }
    }

    /// <inheritdoc/>
    public async Task ReconfigureProviderAsync(
        Guid workspaceId,
        AIProviderType providerType,
        string? endpoint,
        string? apiKey,
        string defaultModelId,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Validate and probe incoming credentials ──────────────────
        // The probe uses INCOMING credentials (not stored), so we call the
        // discovery services directly — not through the stored-config helpers.
        // Existing stored credentials are not read or modified until after
        // all validations pass.

        IReadOnlyList<string> discoveredModels;

        try
        {
            discoveredModels = providerType switch
            {
                AIProviderType.AzureOpenAI => await ProbeAzureCredentialsAsync(
                    endpoint, apiKey, cancellationToken),
                AIProviderType.Ollama => await ProbeOllamaCredentialsAsync(
                    endpoint, cancellationToken),
                _ => throw new ArgumentException(
                         $"Unsupported provider type: {providerType}.",
                         nameof(providerType))
            };
        }
        catch (AIProviderCommunicationException ex)
        {
            // Known provider failure — ex.Message is already sanitised.
            // Never forward raw provider payloads or credential values.
            throw new ProviderReconfigurationException(ex.Message);
        }
        catch (ArgumentException)
        {
            // Unsupported provider type passed validation at the controller — re-throw as-is.
            throw;
        }
        catch (Exception)
        {
            // Unexpected failure during the probe.
            throw new ProviderReconfigurationException(
                "The provider could not be reached. Verify the supplied credentials and try again.");
        }

        // ── Step 2: Validate defaultModelId against the discovered list ──────
        if (!discoveredModels.Contains(defaultModelId, StringComparer.OrdinalIgnoreCase))
        {
            throw new ProviderReconfigurationException(
                $"The model '{defaultModelId}' is not available from the configured provider. " +
                "Select a model from the available list and try again.");
        }

        // ── Step 3: Encrypt the new credentials ──────────────────────────────
        // ValidateAndEncrypt performs provider-type field validation and encrypts as needed.
        // This reuses the existing helper — plaintext values are scoped to this call only.
        var (encryptedEndpoint, encryptedApiKey) =
            ValidateAndEncrypt(providerType, endpoint, apiKey);

        // ── Step 4: Load and update AIProviderConfiguration ──────────────────
        var config = await _repository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        if (config is null)
        {
            throw new InvalidOperationException(
                $"No AI provider configuration found for workspace {workspaceId}.");
        }

        // Update() sets ProviderType, Endpoint, ApiKey, and DefaultModelId atomically.
        // Passing defaultModelId here replaces any stale value left from the previous provider.
        config.Update(providerType, encryptedEndpoint, encryptedApiKey, defaultModelId);

        // Stage the config mutation — do NOT call SaveChangesAsync here.
        await _repository.UpdateAsync(config, cancellationToken);

        // ── Step 5: Commit the mutation atomically ────────────────────────────
        await _workspaceDataAccess.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Private provider-specific model-discovery helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Decrypts the stored Azure OpenAI credentials and delegates to the
    /// <see cref="IAzureOpenAIModelDiscoveryService"/> to fetch deployment names.
    /// Wraps any provider-level failure in <see cref="AIProviderCommunicationException"/>.
    /// </summary>
    /// <remarks>
    /// Decrypted values are intentionally scoped to local variables inside this
    /// method body and are never assigned to fields, logged, or included in
    /// exception messages at any severity level.
    /// </remarks>
    private async Task<IReadOnlyList<string>> GetAzureOpenAIModelsAsync(
        AIProviderConfiguration config,
        CancellationToken cancellationToken)
    {
        // Credentials are decrypted inside this method scope only — they
        // must not escape into logs, fields, or exception messages.
        var endpoint = _encryptionService.Decrypt(config.Endpoint!);
        var apiKey   = _encryptionService.Decrypt(config.ApiKey!);

        try
        {
            return await _azureDiscoveryService.DiscoverModelsAsync(
                endpoint,
                apiKey,
                cancellationToken);
        }
        catch (AIProviderCommunicationException)
        {
            // Already a sanitised exception — re-throw as-is.
            throw;
        }
        catch (Exception ex)
        {
            throw new AIProviderCommunicationException(
                "Failed to communicate with the Azure OpenAI provider.",
                ex);
        }
    }

    /// <summary>
    /// Calls the Ollama <c>GET /api/tags</c> endpoint using the workspace's
    /// configured <c>Endpoint</c> (Ollama server base URL) and extracts the
    /// <c>name</c> field from each entry in the <c>models</c> array.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetOllamaModelsAsync(
        AIProviderConfiguration config,
        CancellationToken cancellationToken)
    {
        var tagsUrl = $"{config.Endpoint!.TrimEnd('/')}/api/tags";

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(tagsUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new AIProviderCommunicationException(
                    $"Ollama returned HTTP {(int)response.StatusCode} when listing models.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc  = JsonDocument.Parse(json);

            var models = doc.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(element => element.GetProperty("name").GetString()!)
                .ToList();

            return models.AsReadOnly();
        }
        catch (AIProviderCommunicationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new AIProviderCommunicationException(
                "Failed to communicate with the Ollama provider.",
                ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Private incoming-credential probe helpers (used by ReconfigureProviderAsync)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Probes the supplied raw Azure OpenAI credentials by calling the discovery service directly.
    /// Plaintext credentials are scoped to this method — they must not escape into fields or logs.
    /// </summary>
    private async Task<IReadOnlyList<string>> ProbeAzureCredentialsAsync(
        string? endpoint,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(endpoint))
            throw new ArgumentException("Endpoint is required for Azure OpenAI provider.", nameof(endpoint));

        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("ApiKey is required for Azure OpenAI provider.", nameof(apiKey));

        try
        {
            return await _azureDiscoveryService.DiscoverModelsAsync(endpoint, apiKey, cancellationToken);
        }
        catch (AIProviderCommunicationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AIProviderCommunicationException(
                "Failed to communicate with the Azure OpenAI provider.", ex);
        }
    }

    /// <summary>
    /// Probes the supplied Ollama base URL by calling <c>GET /api/tags</c>.
    /// </summary>
    private async Task<IReadOnlyList<string>> ProbeOllamaCredentialsAsync(
        string? endpoint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(endpoint))
            throw new ArgumentException("Endpoint is required for Ollama provider.", nameof(endpoint));

        var tagsUrl = $"{endpoint.TrimEnd('/')}/api/tags";

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(tagsUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new AIProviderCommunicationException(
                    $"Ollama returned HTTP {(int)response.StatusCode} when listing models.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc  = System.Text.Json.JsonDocument.Parse(json);

            var models = doc.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(element => element.GetProperty("name").GetString()!)
                .ToList();

            return models.AsReadOnly();
        }
        catch (AIProviderCommunicationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            throw new AIProviderCommunicationException(
                "Failed to communicate with the Ollama provider.", ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates provider-type-specific required fields and, for Azure OpenAI,
    /// encrypts the credential values. All validation is performed before any
    /// encryption call so that an <see cref="ArgumentException"/> is thrown
    /// without touching the encryption service.
    /// </summary>
    /// <returns>
    /// A tuple of the resolved (and conditionally encrypted) credential values
    /// to be assigned to the entity. Null-safe for unused fields.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a required field for the selected provider type is null or empty.
    /// The exception message identifies the parameter name but never echoes its value.
    /// </exception>
    private (string? encryptedEndpoint, string? encryptedApiKey)
        ValidateAndEncrypt(
            AIProviderType providerType,
            string? endpoint,
            string? apiKey)
    {
        if (providerType == AIProviderType.AzureOpenAI)
        {
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentException(
                    "Endpoint is required for Azure OpenAI provider.",
                    nameof(endpoint));

            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException(
                    "ApiKey is required for Azure OpenAI provider.",
                    nameof(apiKey));

            // Credentials are encrypted inside this scope so plaintext strings
            // are not widened to any field or variable outside this method.
            return (
                encryptedEndpoint: _encryptionService.Encrypt(endpoint),
                encryptedApiKey:   _encryptionService.Encrypt(apiKey));
        }

        if (providerType == AIProviderType.Ollama)
        {
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentException(
                    "Endpoint is required for Ollama provider.",
                    nameof(endpoint));

            // Ollama base URL is stored directly in Endpoint (plaintext — not encrypted).
            return (
                encryptedEndpoint: endpoint,
                encryptedApiKey:   null);
        }

        throw new ArgumentException(
            $"Unsupported provider type: {providerType}.",
            nameof(providerType));
    }
}
