namespace Orchestra.Application.Agents.Services;

/// <summary>
/// Service for creating and executing AI agents from database Agent entities.
/// </summary>
public interface IAgentRuntimeService
{
    /// <summary>
    /// Executes an AIAgent for a ticket with the given context prompt.
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent.</param>
    /// <param name="contextPrompt">The execution context prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The text response from the agent execution.</returns>
    Task<string> ExecuteAgentAsync(Guid agentId, string contextPrompt, CancellationToken cancellationToken = default);
}
