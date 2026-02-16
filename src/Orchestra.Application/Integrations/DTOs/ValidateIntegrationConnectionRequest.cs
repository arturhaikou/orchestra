namespace Orchestra.Application.Integrations.DTOs;

/// <summary>
/// Request model for validating integration connection.
/// </summary>
public record ValidateIntegrationConnectionRequest(
    string Provider,
    string Url,
    string? Username,
    string ApiKey,
    string? JiraType = null  // "Cloud" or "OnPremise" for Jira integrations
);
