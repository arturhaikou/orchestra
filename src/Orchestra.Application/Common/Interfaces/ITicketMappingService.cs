using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for mapping external provider data to internal display formats with fallback logic.
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
}