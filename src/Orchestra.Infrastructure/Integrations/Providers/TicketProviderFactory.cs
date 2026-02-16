using Orchestra.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestra.Infrastructure.Integrations.Providers.Jira;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Integrations.Providers;

/// <summary>
/// Factory for creating ticket provider instances based on integration type.
/// Uses dependency injection to resolve provider implementations.
/// </summary>
public class TicketProviderFactory : ITicketProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TicketProviderFactory> _logger;

    public TicketProviderFactory(
        IServiceProvider serviceProvider,
        ILogger<TicketProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Creates a ticket provider instance based on the integration's provider type.
    /// </summary>
    /// <param name="providerType">Provider type enum value.</param>
    /// <returns>Provider implementation instance, or null if not supported.</returns>
    public ITicketProvider? CreateProvider(ProviderType providerType)
    {
        _logger.LogDebug("Creating ticket provider for '{ProviderType}'", providerType);
        
        return providerType switch
        {
            ProviderType.JIRA => _serviceProvider.GetRequiredService<JiraTicketProvider>(),
            // Future providers can be added here:
            // ProviderType.AZURE_DEVOPS => _serviceProvider.GetRequiredService<AzureDevOpsTicketProvider>(),
            // ProviderType.LINEAR => _serviceProvider.GetRequiredService<LinearTicketProvider>(),
            _ => LogAndReturnNull(providerType)
        };
    }

    /// <summary>
    /// Gets all currently supported provider types.
    /// </summary>
    /// <returns>Collection of supported ProviderType enum values.</returns>
    public IEnumerable<ProviderType> GetSupportedProviders()
    {
        return new[] { ProviderType.JIRA };
    }

    private ITicketProvider? LogAndReturnNull(ProviderType providerType)
    {
        _logger.LogWarning("Unsupported ticket provider requested: '{ProviderType}'. Returning null.", 
            providerType);
        return null;
    }
}