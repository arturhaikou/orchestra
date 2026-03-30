using Orchestra.Domain.Entities;

namespace Orchestra.Application.Agents.Services;

/// <summary>
/// Service for building execution context prompts from ticket data.
/// </summary>
public interface IAgentContextBuilder
{
    /// <summary>
    /// Builds an execution prompt containing ticket context and comment history.
    /// For external tickets, fetches data from the external provider.
    /// </summary>
    /// <param name="ticket">The ticket entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Formatted context prompt for agent execution.</returns>
    Task<string> BuildContextPromptAsync(Ticket ticket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a fully enriched execution context prompt for agent execution.
    /// Combines ticket context with integration metadata and, for review agents, project principles.
    /// </summary>
    /// <remarks>
    /// Phase 1: Builds base ticket context (internal comment history or live external ticket data).
    /// Phase 2: Appends a structured [Available Integrations] block when the agent has external tools.
    /// Phase 3: Appends a structured [Project Principles] block when <paramref name="agent"/>
    ///          has a non-null <c>ProjectPrinciples</c> value. No-op for non-review agents.
    /// </remarks>
    /// <param name="ticket">The ticket being executed.</param>
    /// <param name="agent">The agent entity (used for tool lookups and ProjectPrinciples injection).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fully enriched context prompt string.</returns>
    Task<string> BuildAgentContextWithIntegrationsAsync(
        Ticket ticket,
        Agent agent,
        CancellationToken cancellationToken = default);
}
