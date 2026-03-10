using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Common.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.AI.OpenAI;
using CommunityToolkit.Aspire.OllamaSharp;
using OllamaSharp;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Factory for creating IChatClient instances based on provider and model ID.
/// Dynamically instantiates new chat clients for workspace-specific model selections.
/// 
/// Architecture:
/// - For Azure OpenAI: Creates new AzureOpenAIClient + IChatClient wrapper from Azure SDK
/// - For Ollama: Creates new OllamaApiClient + IChatClient wrapper from OllamaSharp SDK
/// - Extensible: New providers (AWS, GCP, etc.) are added as new factory methods
/// 
/// Fallback: If modelId is null or stale, returns the pre-configured default client from DI.
/// </summary>
public sealed class ChatClientResolver : IChatClientResolver
{
    private readonly IChatClient _defaultChatClient;
    private readonly IAIModelRegistry _modelRegistry;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatClientResolver> _logger;
    private readonly string _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatClientResolver"/> class.
    /// </summary>
    /// <param name="defaultChatClient">The startup-configured default IChatClient to use as fallback.</param>
    /// <param name="modelRegistry">The in-memory model registry for checking model availability.</param>
    /// <param name="configuration">Configuration to read provider type and connection strings.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public ChatClientResolver(
        IChatClient defaultChatClient,
        IAIModelRegistry modelRegistry,
        IConfiguration configuration,
        ILogger<ChatClientResolver> logger)
    {
        _defaultChatClient = defaultChatClient;
        _modelRegistry = modelRegistry;
        _configuration = configuration;
        _logger = logger;

        // Read the provider type from configuration (matches Extensions.AddAIProvider logic)
        _provider = _configuration[
            $"{AgentExecutionSettings.SectionName}:{nameof(AgentExecutionSettings.Provider)}"]
            ?? "Azure";
    }

    /// <inheritdoc/>
    public async Task<IChatClient> ResolveChatClientAsync(string? modelId, CancellationToken cancellationToken = default)
    {
        // If no model specified, use default
        if (string.IsNullOrWhiteSpace(modelId))
        {
            _logger.LogInformation("No model specified; using default IChatClient");
            return _defaultChatClient;
        }

        // Check if the specified model is available
        if (!_modelRegistry.IsModelAvailable(modelId))
        {
            // Model is stale/unavailable — silently fall back to default
            _logger.LogWarning(
                "Workspace-configured model '{ModelId}' is no longer available from the AI provider. " +
                "Silently falling back to startup-configured default IChatClient.",
                modelId);
            return _defaultChatClient;
        }

        // Model is available — create a provider-specific chat client for this model
        try
        {
            return _provider switch
            {
                "Azure" => await CreateAzureOpenAIChatClientAsync(modelId, cancellationToken),
                "Ollama" => await CreateOllamaChatClientAsync(modelId, cancellationToken),
                _ => throw new InvalidOperationException(
                    $"Unknown AI provider '{_provider}'. " +
                    $"Valid values are 'Azure' and 'Ollama'.")
            };
        }
        catch (Exception ex)
        {
            // Error creating model-specific client — log and fall back to default
            _logger.LogWarning(
                ex,
                "Error creating model-specific IChatClient for model '{ModelId}' on provider '{Provider}'. " +
                "Falling back to default IChatClient.",
                modelId,
                _provider);
            return _defaultChatClient;
        }
    }

    /// <summary>
    /// Factory method: Creates an Azure OpenAI IChatClient for the specified model deployment name.
    /// Parses the "ai" connection string (format: "Endpoint=https://...;Key=abc123") and
    /// instantiates a new AzureOpenAIClient + wrapper.
    /// </summary>
    private async Task<IChatClient> CreateAzureOpenAIChatClientAsync(string modelId, CancellationToken cancellationToken)
    {
        // Parse connection string: "Endpoint=https://...;Key=abc123"
        var connectionString = _configuration.GetConnectionString("ai") ?? "";
        var dict = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

        var endpoint = dict.TryGetValue("Endpoint", out var ep)
            ? new Uri(ep.TrimEnd('/'))
            : throw new InvalidOperationException(
                "Endpoint not found in 'ai' connection string. " +
                "Expected format: 'Endpoint=https://...;Key=...'");

        var apiKey = dict.TryGetValue("Key", out var k)
            ? k
            : throw new InvalidOperationException(
                "Key not found in 'ai' connection string. " +
                "Expected format: 'Endpoint=https://...;Key=...'");

        // Create the Azure OpenAI client and wrap it with the chat client builder
        var azureClient = new AzureOpenAIClient(endpoint, new Azure.AzureKeyCredential(apiKey));
        var baseChatClient = azureClient.GetChatClient(modelId).AsIChatClient();
        var chatClient = new ChatClientBuilder(baseChatClient)
            .UseFunctionInvocation()
            .UseOpenTelemetry(configure: c => c.EnableSensitiveData = false)
            .Build();

        _logger.LogInformation(
            "Created model-specific Azure OpenAI IChatClient for deployment '{ModelId}' at endpoint {Endpoint}",
            modelId,
            endpoint);

        return await Task.FromResult(chatClient);
    }

    /// <summary>
    /// Factory method: Creates an Ollama IChatClient for the specified model name.
    /// Instantiates a new OllamaApiClient pointing to the configured base URL and
    /// wraps it with the chat client builder.
    /// </summary>
    private async Task<IChatClient> CreateOllamaChatClientAsync(string modelId, CancellationToken cancellationToken)
    {
        // Get the Ollama base URL from configuration (set by Aspire or environment)
        var connectionString = _configuration.GetConnectionString("ai") ?? "http://localhost:11434";

        // Instantiate OllamaApiClient pointing to the Ollama server with the model
        var ollamaClient = new OllamaApiClient(new Uri(connectionString), modelId);

        // Wrap with chat client builder to add middleware
        var chatClient = new ChatClientBuilder(ollamaClient)
            .UseFunctionInvocation()
            .UseOpenTelemetry(configure: c => c.EnableSensitiveData = false)
            .Build();

        _logger.LogInformation(
            "Created model-specific Ollama IChatClient for model '{ModelId}' at base URL {BaseUrl}",
            modelId,
            connectionString);

        return await Task.FromResult(chatClient);
    }

    /// <summary>
    /// Extension point: Add new provider factory methods here.
    /// Example for AWS Bedrock:
    /// ```csharp
    /// private async Task<IChatClient> CreateAwsBedrockChatClientAsync(
    ///     string modelId, CancellationToken cancellationToken)
    /// {
    ///     // Parse AWS credentials, region, model ID
    ///     // Create BedrockClient and wrap with ChatClientBuilder
    ///     // Return wrapped client
    /// }
    /// ```
    ///
    /// Then add to the switch statement in ResolveChatClientAsync:
    /// ```csharp
    /// "Aws" => await CreateAwsBedrockChatClientAsync(modelId, cancellationToken),
    /// ```
    /// </summary>
}
