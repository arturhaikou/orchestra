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
}
