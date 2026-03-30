using Orchestra.Application.CodeReview.Models;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Tools.Services;

/// <summary>
/// Orchestrates the ephemeral code-review sub-agent lifecycle.
/// Called by provider tool services after integration validation.
/// </summary>
public interface ICodeReviewOrchestrationService
{
    /// <summary>
    /// Provisions and runs an ephemeral review sub-agent for the given PR/MR,
    /// then returns the structured result to the calling tool service.
    /// </summary>
    /// <param name="providerType">GITHUB or GITLAB — determines which API client factory is used.</param>
    /// <param name="workspaceId">Workspace that owns the integration.</param>
    /// <param name="integrationId">Raw string integration ID (already validated by the caller).</param>
    /// <param name="prOrMrNumber">Pull request or merge request number as a string.</param>
    /// <param name="modelIdentifier">
    /// Parent agent's LLM model override. Null triggers the system-default deployment in
    /// <see cref="IChatClientResolver"/>.
    /// </param>
    /// <param name="projectPrinciples">
    /// Parent agent's Project Principles text. When non-null, appended to the sub-agent's
    /// base system instructions. Must NOT be logged at any verbosity level.
    /// </param>
    /// <param name="resolvedIntegration">
    /// Fully validated <see cref="Integration"/> record (credentials present).
    /// Passed in from the caller to avoid a second resolver round-trip.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ReviewToolResult"/> — never throws. Exceptions are caught internally
    /// and returned as <c>{ Success = false, Error = "…" }</c>.
    /// </returns>
    Task<ReviewToolResult> ReviewAsync(
        ProviderType providerType,
        Guid workspaceId,
        string integrationId,
        string prOrMrNumber,
        string? modelIdentifier,
        string? projectPrinciples,
        Integration resolvedIntegration,
        CancellationToken cancellationToken = default);
}
