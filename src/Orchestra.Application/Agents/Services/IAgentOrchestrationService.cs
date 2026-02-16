using Orchestra.Application.Agents.DTOs;

namespace Orchestra.Application.Agents.Services;

/// <summary>
/// Service for orchestrating automated agent execution on tickets.
/// </summary>
public interface IAgentOrchestrationService
{
    /// <summary>
    /// Executes an AI agent for a specific ticket, managing the full lifecycle
    /// including status updates, context building, agent execution, and result storage.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with details.</returns>
    Task<AgentExecutionResult> ExecuteAgentForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
