namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when the submitted integration types violate the per-provider
/// allowance rules (e.g., Jira only supports Tracker; Confluence only supports KnowledgeBase).
/// Carries structured data for the controller to build the exact error payload.
/// </summary>
public class InvalidIntegrationTypeForProviderException : Exception
{
    public string ProviderName { get; }
    public IReadOnlyList<string> SubmittedTypes { get; }
    public IReadOnlyList<string> AllowedTypes { get; }

    public InvalidIntegrationTypeForProviderException(
        string providerName,
        IReadOnlyList<string> submittedTypes,
        IReadOnlyList<string> allowedTypes)
        : base($"Provider '{providerName}' does not support the submitted type combination. Allowed types: {string.Join(", ", allowedTypes)}.")
    {
        ProviderName = providerName;
        SubmittedTypes = submittedTypes;
        AllowedTypes = allowedTypes;
    }
}
