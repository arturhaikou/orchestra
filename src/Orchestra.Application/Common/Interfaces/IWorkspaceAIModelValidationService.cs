using System;
using System.Threading;
using System.Threading.Tasks;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service contract for validating AI model identifiers against the currently available
/// models from the active AI provider.
/// </summary>
public interface IWorkspaceAIModelValidationService
{
    /// <summary>
    /// Validates that submitted AI model identifiers exist in the list of available models
    /// from the active AI provider. If any identifier is missing, raises an
    /// <see cref="Orchestra.Application.Common.Exceptions.InvalidAIModelIdentifierException"/>
    /// containing all validation errors.
    /// </summary>
    /// <param name="aiSummarizationModelId">
    /// The optional model identifier for the AI Summarization feature. If null or empty, is skipped.
    /// </param>
    /// <param name="customerSatisfactionAnalysisModelId">
    /// The optional model identifier for the Customer Satisfaction Analysis feature. If null or empty, is skipped.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous validation operation.</returns>
    /// <exception cref="Orchestra.Application.Common.Exceptions.InvalidAIModelIdentifierException">
    /// Raised if one or more submitted model identifiers are not found in the available models list.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Propagated from <see cref="IAIModelListService"/> if the AI provider is misconfigured.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Propagated from <see cref="IAIModelListService"/> if the AI provider is unreachable.
    /// </exception>
    Task ValidateAIModelIdentifiersAsync(
        string? aiSummarizationModelId,
        string? customerSatisfactionAnalysisModelId,
        CancellationToken cancellationToken = default);
}
