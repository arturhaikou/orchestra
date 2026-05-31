using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workflows.Interfaces;

namespace Orchestra.Application.Tickets.Services;

/// <summary>
/// Service implementation for validating ticket assignments.
/// Validates that assigned agents and workflows belong to the same workspace (FR-004).
/// </summary>
public class TicketAssignmentValidationService : ITicketAssignmentValidationService
{
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IWorkflowDefinitionRepository _workflowRepository;

    public TicketAssignmentValidationService(
        IAgentDataAccess agentDataAccess,
        IWorkflowDefinitionRepository workflowRepository)
    {
        _agentDataAccess = agentDataAccess;
        _workflowRepository = workflowRepository;
    }

    /// <summary>
    /// Validates agent exists and returns its workspace ID.
    /// Extracted logic from CreateTicketAsync, UpdateInternalTicketAsync, UpdateExternalTicketAsync.
    /// </summary>
    /// <param name="agentId">The agent ID to validate (null is allowed for unassignment)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The agent's workspace ID if agentId is provided, null if agentId is null</returns>
    /// <exception cref="ArgumentException">Thrown when agentId is provided but agent not found</exception>
    public async Task<Guid?> ValidateAndGetAgentWorkspaceAsync(Guid? agentId, CancellationToken cancellationToken = default)
    {
        // If no agent is being assigned, return null (unassignment case)
        if (!agentId.HasValue)
        {
            return null;
        }

        // Fetch agent from database
        var agent = await _agentDataAccess.GetByIdAsync(agentId.Value, cancellationToken);

        // FR-004: Ensure agent exists before proceeding
        if (agent == null)
        {
            throw new ArgumentException(
                $"Agent with ID {agentId.Value} not found.");
        }

        // Extract and return workspace ID for domain-level validation
        return agent.WorkspaceId;
    }

    /// <summary>
    /// Validates workflow exists and returns its workspace ID.
    /// </summary>
    /// <param name="workflowId">The workflow ID to validate (null is allowed for unassignment)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The workflow's workspace ID if workflowId is provided, null if workflowId is null</returns>
    /// <exception cref="ArgumentException">Thrown when workflowId is provided but workflow not found</exception>
    public async Task<Guid?> ValidateAndGetWorkflowWorkspaceAsync(Guid? workflowId, CancellationToken cancellationToken = default)
    {
        if (!workflowId.HasValue)
        {
            return null;
        }

        var workflow = await _workflowRepository.GetByIdAsync(workflowId.Value, cancellationToken);

        if (workflow is null)
        {
            throw new ArgumentException(
                $"Workflow with ID {workflowId.Value} not found.");
        }

        return workflow.WorkspaceId;
    }
}
