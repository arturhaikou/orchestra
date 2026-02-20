using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Orchestra.Application.Tickets.Services;

public class TicketMaterializationService : ITicketMaterializationService
{
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly ILogger<TicketMaterializationService> _logger;

    public TicketMaterializationService(
        ITicketDataAccess ticketDataAccess,
        ILogger<TicketMaterializationService> logger)
    {
        _ticketDataAccess = ticketDataAccess;
        _logger = logger;
    }

    public async Task<Ticket> MaterializeFromExternalAsync(
        Guid integrationId,
        string externalTicketId,
        Guid workspaceId,
        ExternalTicketDto externalTicket,
        Guid? assignedAgentId,
        Guid? assignedWorkflowId,
        CancellationToken cancellationToken)
    {
        // Map external priority to internal priority (by value)
        var mappedPriority = await MapExternalPriorityToInternalAsync(
            externalTicket.PriorityValue,
            cancellationToken);

        if (mappedPriority == null)
        {
            throw new InvalidOperationException("No priorities found in the system.");
        }

        // Status GUIDs from seeding
        var newStatusId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        // Create materialized ticket
        var materializedTicket = Ticket.MaterializeFromExternal(
            workspaceId,
            integrationId,
            externalTicketId,
            string.Empty,
            string.Empty,
            statusId: newStatusId,
            priorityId: mappedPriority.Id,
            assignedAgentId: assignedAgentId,
            assignedWorkflowId: assignedWorkflowId);

        _logger.LogInformation(
            "Materialized external ticket {ExternalTicketId} with priority {PriorityName}",
            externalTicketId, mappedPriority.Name);

        return materializedTicket;
    }

    public async Task<TicketPriority> MapExternalPriorityToInternalAsync(
        int externalPriorityValue,
        CancellationToken cancellationToken)
    {
        // Get all internal priorities
        var allPriorities = await _ticketDataAccess.GetAllPrioritiesAsync(cancellationToken);

        if (allPriorities == null || allPriorities.Count == 0)
        {
            throw new InvalidOperationException("No priorities found in the system.");
        }

        // Map external priority to closest internal priority by value (nearest neighbor)
        var mappedPriority = allPriorities
            .OrderBy(p => Math.Abs(p.Value - externalPriorityValue))
            .FirstOrDefault();

        if (mappedPriority == null)
        {
            throw new InvalidOperationException("No priorities found in the system.");
        }

        _logger.LogDebug(
            "Mapped external priority value {ExternalValue} to internal priority {PriorityName} (value: {InternalValue})",
            externalPriorityValue, mappedPriority.Name, mappedPriority.Value);

        return mappedPriority;
    }
}
