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
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of AIFunction instances ready for agent integration.</returns>
    Task<IEnumerable<AIFunction>> GetAgentToolsAsync(
        Guid agentId, 
        CancellationToken cancellationToken = default);
}