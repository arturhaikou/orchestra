using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Common.Exceptions;

namespace Orchestra.Application.Workspaces.Services;

/// <summary>
/// Specialist service for validating AI model identifiers during workspace creation and updates.
/// Coordinates with IAIModelListService to verify that submitted model identifiers exist in the
/// active AI provider's catalogue. All validation errors are collected and raised together
/// in a single exception.
/// </summary>
public sealed class WorkspaceAIModelValidationService : IWorkspaceAIModelValidationService
{
    private readonly IAIModelListService _aiModelListService;

    public WorkspaceAIModelValidationService(IAIModelListService aiModelListService)
    {
        _aiModelListService = aiModelListService;
    }

    /// <inheritdoc/>
    public async Task ValidateAIModelIdentifiersAsync(
        string? aiSummarizationModelId,
        string? customerSatisfactionAnalysisModelId,
        CancellationToken cancellationToken = default)
    {
        // If both model identifiers are omitted, no validation is needed.
        if (string.IsNullOrWhiteSpace(aiSummarizationModelId) &&
            string.IsNullOrWhiteSpace(customerSatisfactionAnalysisModelId))
        {
            return;
        }

        // Fetch the list of available models from the AI provider.
        // If the provider is unreachable or misconfigured, exceptions propagate to the caller
        // (HttpRequestException or InvalidOperationException), which the controller maps to 500.
        var availableModels = await _aiModelListService.GetAvailableModelsAsync(cancellationToken);

        // Collect all validation errors before raising.
        var invalidModels = new Dictionary<string, string>();

        // Check AI Summarization model identifier.
        if (!string.IsNullOrWhiteSpace(aiSummarizationModelId) &&
            !availableModels.Contains(aiSummarizationModelId))
        {
            invalidModels["AI Summarization"] = aiSummarizationModelId;
        }

        // Check Customer Satisfaction Analysis model identifier.
        if (!string.IsNullOrWhiteSpace(customerSatisfactionAnalysisModelId) &&
            !availableModels.Contains(customerSatisfactionAnalysisModelId))
        {
            invalidModels["Customer Satisfaction Analysis"] = customerSatisfactionAnalysisModelId;
        }

        // If any errors were collected, raise an exception describing all of them.
        if (invalidModels.Count > 0)
        {
            throw new InvalidAIModelIdentifierException(invalidModels);
        }
    }
}
