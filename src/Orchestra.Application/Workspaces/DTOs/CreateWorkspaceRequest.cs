using Orchestra.Domain.Enums;

namespace Orchestra.Application.Workspaces.DTOs;

public record CreateWorkspaceRequest
{
    public required string Name { get; init; }
    public bool? IsAiSummarizationEnabled { get; init; }
    public bool? IsCustomerSatisfactionAnalysisEnabled { get; init; }
    public string? AiSummarizationModelId { get; init; }
    public string? CustomerSatisfactionAnalysisModelId { get; init; }

    // AI Provider configuration (required for workspace creation)
    public AIProviderType? ProviderType { get; init; }

    /// <summary>Azure OpenAI endpoint URL. Required when ProviderType = AzureOpenAI.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Azure OpenAI API key. Required when ProviderType = AzureOpenAI.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Optional default model identifier for AI operations in this workspace.</summary>
    public string? DefaultModelId { get; init; }
}