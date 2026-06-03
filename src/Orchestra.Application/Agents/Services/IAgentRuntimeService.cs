using Orchestra.Application.Agents.Models;
using Orchestra.Application.Jobs.DTOs;

namespace Orchestra.Application.Agents.Services;

/// <summary>
/// Service for creating and executing AI agents from database Agent entities.
/// </summary>
public interface IAgentRuntimeService
{
    /// <summary>
    /// Executes an AIAgent for a ticket with the given context prompt.
    /// All context enrichment (ticket data + integration metadata) should be prepared
    /// by the caller before invocation via BuildAgentContextWithIntegrationsAsync().
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent.</param>
    /// <param name="contextInput">
    /// The fully enriched context input, containing the text prompt (ticket context +
    /// integration metadata blocks) and any image references extracted from the ticket.
    /// </param>
    /// <param name="agentModel">
    /// The LLM model identifier stored on the agent entity, or null if the agent has
    /// no model override. When null, the implementation must fall back to the
    /// system-configured deployment name from application settings.
    /// </param>
    /// <param name="projectPrinciples">
    /// The parent agent's Project Principles text (nullable). Forwarded to
    /// <c>IToolRetrieverService</c> for capture in the review-action closure.
    /// Must NOT be logged at any verbosity level.
    /// </param>
    /// <param name="jobContext">The job context for tracking job execution (nullable).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the text response from agent execution and the optional job ID.</returns>
    Task<(string ResponseText, Guid? JobId)> ExecuteAgentAsync(
        Guid agentId,
        AgentContextInput contextInput,
        string? agentModel = null,
        string? projectPrinciples = null,
        JobContext? jobContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a suspended background job agent from its saved session snapshot and
    /// continues execution, injecting the user's answers as a new message.
    /// </summary>
    Task ExecuteRestoredAgentAsync(
        Guid jobId,
        Guid questionId,
        string answersJson,
        CancellationToken cancellationToken = default);
}
