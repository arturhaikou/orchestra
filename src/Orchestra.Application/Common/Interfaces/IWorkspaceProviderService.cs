using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Primary entry point for all AI provider configuration operations within a workspace.
/// Owns the full provider lifecycle: create, update, and model discovery.
/// </summary>
public interface IWorkspaceProviderService
{
    /// <summary>
    /// Creates a new AI provider configuration for the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace to configure.</param>
    /// <param name="providerType">The AI provider to use (Azure OpenAI or Ollama).</param>
    /// <param name="endpoint">
    /// The provider URL in plaintext. For AzureOpenAI: the Azure resource endpoint URL.
    /// For Ollama: the Ollama server base URL. Required for all provider types.
    /// </param>
    /// <param name="apiKey">
    /// Azure OpenAI API key in plaintext. Required when <paramref name="providerType"/>
    /// is <see cref="AIProviderType.AzureOpenAI"/>; pass <see langword="null"/> for Ollama.
    /// </param>
    /// <param name="defaultModelId">
    /// The default model identifier for this workspace. Required (non-null, non-whitespace)
    /// when <paramref name="providerType"/> is <see cref="AIProviderType.Ollama"/>;
    /// pass <see langword="null"/> or supply a value for AzureOpenAI (not validated at creation time).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The <see cref="Guid"/> identifier of the newly created
    /// <c>AIProviderConfiguration</c> entity, so the caller can link it to the workspace record.
    /// </returns>
    Task<Guid> CreateProviderConfigAsync(
        Guid workspaceId,
        AIProviderType providerType,
        string? endpoint,
        string? apiKey,
        string? defaultModelId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the AI provider configuration for the specified workspace.
    /// Pass <see langword="null"/> for <paramref name="defaultModelId"/> to explicitly clear
    /// a stale model identifier when switching providers.
    /// </summary>
    /// <param name="workspaceId">The workspace whose configuration should be updated.</param>
    /// <param name="providerType">The new AI provider to use.</param>
    /// <param name="endpoint">
    /// The provider URL in plaintext. For AzureOpenAI: the Azure resource endpoint URL.
    /// For Ollama: the Ollama server base URL. Required for all provider types.
    /// </param>
    /// <param name="apiKey">Azure OpenAI API key in plaintext; <see langword="null"/> for Ollama.</param>
    /// <param name="defaultModelId">
    /// The default model identifier for this workspace. Pass <see langword="null"/> to explicitly clear
    /// a stale model identifier when switching providers.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateProviderConfigAsync(
        Guid workspaceId,
        AIProviderType providerType,
        string? endpoint,
        string? apiKey,
        string? defaultModelId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the names of AI models available for the specified workspace,
    /// based on the workspace's configured provider.
    /// </summary>
    /// <param name="workspaceId">The workspace to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A read-only list of model names. For Azure OpenAI this is deployment names;
    /// for Ollama this is pulled model identifiers.
    /// </returns>
    Task<IReadOnlyList<string>> GetAvailableModelsAsync(Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>
    /// Probes the workspace's stored AI provider credentials by issuing a live
    /// connectivity check and returns a structured validation result.
    /// </summary>
    /// <param name="workspaceId">The workspace to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ProviderValidationResult"/> describing the outcome of the probe, or
    /// <see langword="null"/> when the workspace has no stored <c>AIProviderConfiguration</c>.
    /// </returns>
    /// <remarks>
    /// This operation is read-only; it never mutates any stored data.
    /// Decrypted credentials must remain scoped to the in-process call and must never
    /// surface in response bodies, logs, or traces.
    /// </remarks>
    Task<ProviderValidationResult?> ValidateProviderAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates the supplied credentials against the live provider, then atomically updates
    /// the workspace's <c>AIProviderConfiguration</c> and <c>DefaultModelId</c>.
    /// </summary>
    /// <param name="workspaceId">The workspace whose provider configuration should be replaced.</param>
    /// <param name="providerType">The new provider type to configure.</param>
    /// <param name="endpoint">
    /// The provider URL in plaintext. For AzureOpenAI: the Azure resource endpoint URL.
    /// For Ollama: the Ollama server base URL. Required for all provider types.
    /// </param>
    /// <param name="apiKey">
    /// Plaintext Azure OpenAI API key. Required when <paramref name="providerType"/>
    /// is <see cref="AIProviderType.AzureOpenAI"/>; pass <see langword="null"/> for Ollama.
    /// </param>
    /// <param name="defaultModelId">
    /// The model identifier to set as the workspace default. Must be present in the live
    /// provider's model list; otherwise a <see cref="ProviderReconfigurationException"/> is thrown.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ProviderReconfigurationException">
    /// Thrown when the live credential probe fails, or when <paramref name="defaultModelId"/>
    /// is not found in the validated model list. Existing stored credentials are left unchanged.
    /// Maps to <c>422 Unprocessable Entity</c> at the API layer.
    /// </exception>
    /// <remarks>
    /// The incoming <paramref name="apiKey"/> must not be written to logs, traces, response
    /// bodies, or exception messages at any point in the call stack.
    /// </remarks>
    Task ReconfigureProviderAsync(
        Guid workspaceId,
        AIProviderType providerType,
        string? endpoint,
        string? apiKey,
        string defaultModelId,
        CancellationToken cancellationToken);
}
