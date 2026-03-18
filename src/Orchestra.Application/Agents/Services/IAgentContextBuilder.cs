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
    /// Combines ticket context with integration metadata scoped to the agent's external tool provider types.
    /// </summary>
    /// <remarks>
    /// This method:
    /// 1. Loads the agent's external tool provider types
    /// 2. Fetches active integrations scoped to the workspace and those provider types
    /// 3. Builds ticket context (internal or external)
    /// 4. Appends a structured integration context block listing available integrations
    /// 
    /// The integration context block allows the LLM to autonomously resolve the correct integrationId
    /// when invoking external tools. Only non-sensitive metadata (ID, Name, Provider) is included.
    /// If the agent has no external tools, no integration context block is appended.
    /// </remarks>
    /// <param name="ticket">The ticket entity.</param>
    /// <param name="agentId">The agent's unique identifier, used to load its external tool provider types.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Fully enriched context prompt for agent execution (ticket + integrations).</returns>
    Task<string> BuildAgentContextWithIntegrationsAsync(Ticket ticket, Guid agentId, CancellationToken cancellationToken = default);
}
