using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Service for orchestrating automated agent execution on tickets.
/// Manages the full lifecycle: status updates, context building, agent execution, and result storage.
/// </summary>
public class AgentOrchestrationService : IAgentOrchestrationService
{
    private readonly IAgentRuntimeService _agentRuntimeService;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly IAgentContextBuilder _agentContextBuilder;
    private readonly ILogger<AgentOrchestrationService> _logger;

    // Status GUIDs from seeding
    private static readonly Guid ToDoStatusId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid InProgressStatusId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid CompletedStatusId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    public AgentOrchestrationService(
        IAgentRuntimeService agentRuntimeService,
        IAgentDataAccess agentDataAccess,
        ITicketDataAccess ticketDataAccess,
        IAgentContextBuilder agentContextBuilder,
        ILogger<AgentOrchestrationService> logger)
    {
        _agentRuntimeService = agentRuntimeService;
        _agentDataAccess = agentDataAccess;
        _ticketDataAccess = ticketDataAccess;
        _agentContextBuilder = agentContextBuilder;
        _logger = logger;
    }

    public async Task<AgentExecutionResult> ExecuteAgentForTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        Ticket? ticket = null;
        Agent? agentEntity = null;

        try
        {
            // 1. Load ticket
            ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketId, cancellationToken);
            if (ticket == null)
            {
                _logger.LogWarning("Ticket {TicketId} not found", ticketId);
                return AgentExecutionResult.Failure("Ticket not found");
            }

            if (!ticket.AssignedAgentId.HasValue)
            {
                _logger.LogWarning("Ticket {TicketId} has no assigned agent", ticketId);
                return AgentExecutionResult.Failure("No agent assigned to ticket");
            }

            // 2. Load agent entity
            agentEntity = await _agentDataAccess.GetByIdAsync(
                ticket.AssignedAgentId.Value,
                cancellationToken);

            if (agentEntity == null)
            {
                _logger.LogWarning(
                    "Agent {AgentId} assigned to ticket {TicketId} not found",
                    ticket.AssignedAgentId.Value,
                    ticketId);
                return AgentExecutionResult.Failure("Assigned agent not found");
            }

            _logger.LogInformation(
                "Starting agent execution: Agent {AgentName} ({AgentId}) on Ticket {TicketId}",
                agentEntity.Name,
                agentEntity.Id,
                ticketId);

            // 3. Set agent status to Busy
            agentEntity.UpdateStatus(AgentStatus.Busy);
            await _agentDataAccess.UpdateAsync(agentEntity, cancellationToken);

            // 4. Initialize status for legacy tickets and set to InProgress
            // For legacy external tickets (StatusId=null), set to "To Do" first
            if (!ticket.StatusId.HasValue)
            {
                ticket.UpdateStatus(ToDoStatusId);
            }
            
            ticket.UpdateStatus(InProgressStatusId);
            await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

            // 5. Build context prompt
            var contextPrompt = await _agentContextBuilder.BuildContextPromptAsync(
                ticket,
                cancellationToken);

            _logger.LogDebug(
                "Built context prompt for ticket {TicketId}: {PromptLength} characters",
                ticketId,
                contextPrompt.Length);

            // 6. Execute AI agent
            _logger.LogInformation(
                "Executing agent {AgentName}",
                agentEntity.Name);

            var responseText = await _agentRuntimeService.ExecuteAgentAsync(
                agentEntity.Id,
                contextPrompt,
                cancellationToken);

            _logger.LogInformation(
                "Agent execution completed successfully for ticket {TicketId}",
                ticketId);

            // 7. Success - Update ticket status to Completed
            ticket.UpdateStatus(CompletedStatusId);
            await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

            // 8. Add result comment
            var comment = TicketComment.Create(
                ticketId,
                agentEntity.Name,
                responseText);

            await _ticketDataAccess.AddCommentAsync(comment, cancellationToken);

            return AgentExecutionResult.Success(responseText);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error executing agent for ticket {TicketId}",
                ticketId);

            // Error handling - Add error comment
            if (ticket != null && agentEntity != null)
            {
                try
                {
                    var errorComment = TicketComment.Create(
                        ticketId,
                        agentEntity.Name,
                        $"❌ Execution failed: {ex.Message}");

                    await _ticketDataAccess.AddCommentAsync(errorComment, cancellationToken);

                    // Revert status to To Do for retry
                    ticket.UpdateStatus(ToDoStatusId);
                    await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(
                        innerEx,
                        "Failed to add error comment for ticket {TicketId}",
                        ticketId);
                }
            }

            return AgentExecutionResult.Failure(ex.Message);
        }
        finally
        {
            // Always set agent back to Idle
            if (agentEntity != null)
            {
                try
                {
                    agentEntity.UpdateStatus(AgentStatus.Idle);
                    await _agentDataAccess.UpdateAsync(agentEntity, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to set agent {AgentId} back to Idle status",
                        agentEntity.Id);
                }
            }
        }
    }
}
