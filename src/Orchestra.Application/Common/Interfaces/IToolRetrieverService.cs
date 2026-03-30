using Microsoft.Extensions.AI;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for retrieving and converting agent tools into AIFunction instances
/// for Microsoft Agent Framework integration.
/// </summary>
public interface IToolRetrieverService
{
    /// <summary>
    /// Retrieves all AIFunction tools assigned to an agent.
    /// For <c>review_pull_request</c> and <c>review_merge_request</c> actions, returns a
    /// closure-based wrapper that captures <paramref name="modelIdentifier"/> and
    /// <paramref name="projectPrinciples"/> so the LLM never sees them as addressable parameters.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="modelIdentifier">
    /// The parent agent's LLM model override (nullable). Captured in the review-action closure
    /// and forwarded to <c>ICodeReviewOrchestrationService</c> at invocation time.
    /// </param>
    /// <param name="projectPrinciples">
    /// The parent agent's Project Principles text (nullable). Captured in the review-action
    /// closure. Must NOT be logged at any verbosity level.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of AIFunction instances ready for agent integration.</returns>
    Task<IEnumerable<AIFunction>> GetAgentToolsAsync(
        Guid agentId,
        string? modelIdentifier = null,
        string? projectPrinciples = null,
        CancellationToken cancellationToken = default);
}