using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Production implementation of <see cref="IAIProviderResolver"/>.
/// Resolves a workspace-scoped <see cref="IChatClient"/> by reading the workspace's
/// <c>AIProviderConfiguration</c> from the repository and constructing the appropriate provider client
/// with the requested <paramref name="modelId"/> baked in at construction time.
/// A fresh client is returned per invocation — no caching is performed here.
/// </summary>
public sealed class AIProviderResolver : IAIProviderResolver
{
    private readonly IWorkspaceAIProviderRepository _repository;
    private readonly IProviderCredentialEncryptionService _encryptionService;

    public AIProviderResolver(
        IWorkspaceAIProviderRepository repository,
        IProviderCredentialEncryptionService encryptionService)
    {
        _repository = repository;
        _encryptionService = encryptionService;
    }

    /// <inheritdoc/>
    public async Task<IChatClient> ResolveAsync(
        Guid workspaceId,
        string modelId,
        CancellationToken cancellationToken)
    {
        var config = await _repository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);

        if (config is null)
        {
            throw new InvalidOperationException(
                $"No AI provider configuration found for workspace {workspaceId}");
        }

        return config.ProviderType switch
        {
            AIProviderType.AzureOpenAI => BuildAzureOpenAIClient(
                                             config.Endpoint!,
                                             config.ApiKey!,
                                             modelId),
            AIProviderType.Ollama => BuildOllamaClient(config.Endpoint!, modelId),
            _ => throw new InvalidOperationException(
                                             $"Unsupported AI provider type: {config.ProviderType}")
        };
    }

    // Credentials are decrypted inside this private helper so the decrypted strings
    // never leave the scope of AzureOpenAIClient construction.
    // For Azure OpenAI, modelId IS the deployment name — in practice the same string as the model
    // name (e.g. "gpt-4o"), because Azure deployments are typically named after the model.
    private IChatClient BuildAzureOpenAIClient(
        string encryptedEndpoint,
        string encryptedApiKey,
        string modelId)
    {
        return new AzureOpenAIClient(
            new Uri(_encryptionService.Decrypt(encryptedEndpoint)),
            new AzureKeyCredential(_encryptionService.Decrypt(encryptedApiKey)))
            .GetChatClient(deploymentName: modelId)
            .AsIChatClient();
    }

    // OllamaApiClient from OllamaSharp 5.x accepts a model tag in the constructor.
    // The model is baked in — callers do not need to set ChatOptions.ModelId.
    private static IChatClient BuildOllamaClient(string ollamaBaseUrl, string modelId)
    {
        return new OllamaApiClient(new Uri(ollamaBaseUrl), modelId);
    }
}
