using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Data access interface for querying tickets eligible for agent execution.
/// </summary>
public interface ITicketAgentExecutionDataAccess
{
    /// <summary>
    /// Gets internal tickets ready for agent execution (status = "To Do" and agent assigned).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tickets ready for processing.</returns>
    Task<List<Ticket>> GetInternalTicketsReadyForAgentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets materialized external tickets ready for agent execution (agent assigned).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tickets ready for processing.</returns>
    Task<List<Ticket>> GetExternalMaterializedTicketsReadyForAgentAsync(CancellationToken cancellationToken = default);
}
