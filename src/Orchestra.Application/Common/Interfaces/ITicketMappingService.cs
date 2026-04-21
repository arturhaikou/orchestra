using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Application.Tickets.DTOs;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for mapping external provider data to internal display formats and mapping entities to DTOs.
/// </summary>
public interface ITicketMappingService
{
    /// <summary>
    /// Maps external status name to display string with fallback.
    /// </summary>
    /// <param name="externalStatus">Status name from external provider (e.g., "In Progress", "Done").</param>
    /// <param name="providerType">Provider type for provider-specific mapping.</param>
    /// <returns>
    /// Mapped display status name. Defaults to "To Do" if unmapped or empty.
    /// </returns>
    /// <remarks>
    /// Provider-specific mappings normalize different status naming conventions.
    /// For example, Jira "In Progress" maps to "InProgress", Jira "Done" maps to "Completed".
    /// Fallback value ensures graceful handling of unknown statuses.
    /// </remarks>
    string MapStatusToDisplay(string externalStatus, ProviderType providerType);
    
    /// <summary>
    /// Maps external priority name to display string with fallback.
    /// </summary>
    /// <param name="externalPriority">Priority name from external provider (e.g., "Highest", "Blocker").</param>
    /// <param name="providerType">Provider type for provider-specific mapping.</param>
    /// <returns>
    /// Mapped display priority name. Defaults to "Medium" if unmapped or empty.
    /// </returns>
    /// <remarks>
    /// Provider-specific mappings normalize different priority naming conventions.
    /// For example, Jira "Highest" and "Blocker" both map to "Critical".
    /// Fallback value ensures graceful handling of unknown priorities.
    /// </remarks>
    string MapPriorityToDisplay(string externalPriority, ProviderType providerType);
    
    /// <summary>
    /// Constructs external ticket URL from integration base URL and ticket ID.
    /// </summary>
    /// <param name="integration">Integration with base URL and provider type.</param>
    /// <param name="externalTicketId">External ticket identifier (e.g., "PROJ-123").</param>
    /// <returns>
    /// Full URL to view the ticket in the external system.
    /// </returns>
    /// <remarks>
    /// URL patterns are provider-specific:
    /// - Jira: {baseUrl}/browse/{ticketId}
    /// - Azure DevOps: {baseUrl}/_workitems/edit/{ticketId}
    /// - Default: {baseUrl}/ticket/{ticketId}
    /// </remarks>
    string BuildExternalUrl(Integration integration, string externalTicketId);

    /// <summary>
    /// Maps an internal ticket entity to TicketDto for display.
    /// Handles composite ID generation for materialized external tickets.
    /// </summary>
    /// <param name="ticket">The ticket entity to map</param>
    /// <param name="statusLookup">Dictionary of TicketStatus indexed by ID</param>
    /// <param name="priorityLookup">Dictionary of TicketPriority indexed by ID</param>
    /// <param name="comments">Pre-fetched comments for the ticket</param>
    /// <returns>Mapped TicketDto with proper ID format and status/priority details</returns>
    TicketDto MapInternalTicketToDto(
        Ticket ticket,
        Dictionary<Guid, TicketStatus> statusLookup,
        Dictionary<Guid, TicketPriority> priorityLookup,
        IEnumerable<TicketComment> comments);

    /// <summary>
    /// Maps an external ticket from provider to TicketDto with optional materialized data merging.
    /// </summary>
    /// <param name="integration">The integration providing the ticket</param>
    /// <param name="externalTicket">The external ticket DTO from provider</param>
    /// <param name="materializedTicket">Optional materialized DB record with assignments</param>
    /// <param name="statusLookup">Dictionary of internal TicketStatus indexed by ID (for materialized status override)</param>
    /// <param name="priorityLookup">Dictionary of internal TicketPriority indexed by ID (for materialized priority override)</param>
    /// <param name="materializedComments">Optional pre-fetched comments for materialized ticket</param>
    /// <returns>Mapped TicketDto in composite ID format with merged assignments</returns>
    Task<TicketDto> MapExternalTicketToDtoAsync(
        Integration integration,
        ExternalTicketDto externalTicket,
        Ticket? materializedTicket,
        Dictionary<Guid, TicketStatus> statusLookup,
        Dictionary<Guid, TicketPriority> priorityLookup,
        IEnumerable<TicketComment>? materializedComments = null);
}