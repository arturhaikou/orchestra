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
    /// <param name="contextPrompt">
    /// The fully enriched execution context prompt, including ticket context and any
    /// integration metadata blocks appended by the caller.
    /// </param>
    /// <param name="agentModel">
    /// The LLM model identifier stored on the agent entity, or null if the agent has
    /// no model override. When null, the implementation must fall back to the
    /// system-configured deployment name from application settings.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The text response from the agent execution.</returns>
    Task<string> ExecuteAgentAsync(
        Guid agentId,
        string contextPrompt,
        string? agentModel = null,
        CancellationToken cancellationToken = default);
}
