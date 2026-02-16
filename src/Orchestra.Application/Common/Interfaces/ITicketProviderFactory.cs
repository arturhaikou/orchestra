namespace Orchestra.Application.Common.Interfaces;

using Orchestra.Domain.Enums;

/// <summary>
/// Factory for instantiating ticket provider implementations based on integration type.
/// Enables runtime provider selection without coupling application logic to specific providers.
/// </summary>
public interface ITicketProviderFactory
{
    /// <summary>
    /// Creates a ticket provider instance based on the integration's provider type.
    /// </summary>
    /// <param name="providerType">
    /// Provider type enum value. Examples: ProviderType.JIRA, ProviderType.AZURE_DEVOPS.
    /// </param>
    /// <returns>
    /// Provider implementation instance, or null if the provider is not supported.
    /// </returns>
    /// <remarks>
    /// Returning null allows graceful degradation when fetching tickets from multiple integrations.
    /// The application can log a warning and continue with other providers.
    /// </remarks>
    ITicketProvider? CreateProvider(ProviderType providerType);
    
    /// <summary>
    /// Gets all currently supported provider types.
    /// </summary>
    /// <returns>
    /// Collection of supported ProviderType enum values.
    /// </returns>
    /// <remarks>
    /// Useful for integration validation and UI dropdown population.
    /// </remarks>
    IEnumerable<ProviderType> GetSupportedProviders();
}