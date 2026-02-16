using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Orchestra.Infrastructure.Integrations;

/// <summary>
/// Service for mapping external provider data to internal display formats with fallback logic.
/// </summary>
public class TicketMappingService : ITicketMappingService
{
    private readonly ILogger<TicketMappingService> _logger;

    public TicketMappingService(ILogger<TicketMappingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Maps external status name to display string with fallback.
    /// </summary>
    public string MapStatusToDisplay(string externalStatus, ProviderType providerType)
    {
        if (string.IsNullOrWhiteSpace(externalStatus))
        {
            _logger.LogWarning("External status is null or whitespace, using fallback 'To Do'");
            return "To Do";
        }

        return providerType switch
        {
            ProviderType.JIRA => MapJiraStatus(externalStatus),
            _ => externalStatus
        };
    }

    private string MapJiraStatus(string jiraStatus)
    {
        var normalizedStatus = jiraStatus.ToLowerInvariant();

        switch (normalizedStatus)
        {
            case "to do":
            case "backlog":
            case "open":
                return "To Do";
            case "in progress":
            case "in review":
            case "review":
                return "InProgress";
            case "done":
            case "closed":
            case "resolved":
            case "complete":
                return "Completed";
            case "new":
                return "New";
            default:
                _logger.LogWarning("Unknown Jira status '{JiraStatus}', using fallback 'To Do'", jiraStatus);
                return "To Do";
        }
    }

    private string MapJiraPriority(string jiraPriority)
    {
        var normalizedPriority = jiraPriority.ToLowerInvariant();

        switch (normalizedPriority)
        {
            case "highest":
            case "blocker":
                return "Critical";
            case "high":
                return "High";
            case "medium":
            case "normal":
                return "Medium";
            case "low":
            case "lowest":
            case "trivial":
                return "Low";
            default:
                _logger.LogWarning("Unknown Jira priority '{JiraPriority}', using fallback 'Medium'", jiraPriority);
                return "Medium";
        }
    }

    /// <summary>
    /// Maps external priority name to display string with fallback.
    /// </summary>
    public string MapPriorityToDisplay(string externalPriority, ProviderType providerType)
    {
        if (string.IsNullOrWhiteSpace(externalPriority))
        {
            _logger.LogWarning("External priority is null or whitespace, using fallback 'Medium'");
            return "Medium";
        }

        return providerType switch
        {
            ProviderType.JIRA => MapJiraPriority(externalPriority),
            _ => externalPriority
        };
    }

    /// <summary>
    /// Constructs external ticket URL from integration base URL and ticket ID.
    /// </summary>
    public string BuildExternalUrl(Integration integration, string externalTicketId)
    {
        var baseUrl = integration.Url?.TrimEnd('/') ?? string.Empty;

        return integration.Provider switch
        {
            ProviderType.JIRA => $"{baseUrl}/browse/{externalTicketId}",
            ProviderType.AZURE_DEVOPS => $"{baseUrl}/_workitems/edit/{externalTicketId}",
            _ => $"{baseUrl}/ticket/{externalTicketId}"
        };
    }
}