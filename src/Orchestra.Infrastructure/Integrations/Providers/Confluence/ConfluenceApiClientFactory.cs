using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Domain.Utilities;

namespace Orchestra.Infrastructure.Integrations.Providers.Confluence;

/// <summary>
/// Factory for creating Confluence API client instances based on Confluence instance type (Cloud vs On-Premise).
/// Handles HTTP client configuration, authentication, and version-specific routing.
/// </summary>
public class ConfluenceApiClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly ILoggerFactory _loggerFactory;

    public ConfluenceApiClientFactory(
        IHttpClientFactory httpClientFactory,
        ICredentialEncryptionService credentialEncryptionService,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _credentialEncryptionService = credentialEncryptionService;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a Confluence API client for the specified integration.
    /// </summary>
    /// <param name="integration">The integration configuration.</param>
    /// <returns>An IConfluenceApiClient implementation (Cloud or On-Premise).</returns>
    /// <exception cref="ArgumentException">Thrown if URL or API key is missing.</exception>
    /// <exception cref="InvalidOperationException">Thrown if ConfluenceType is not set.</exception>
    public IConfluenceApiClient CreateClient(Integration integration)
    {
        if (string.IsNullOrEmpty(integration.Url))
        {
            throw new ArgumentException("Integration URL is required for Confluence API calls.", nameof(integration.Url));
        }

        if (string.IsNullOrEmpty(integration.EncryptedApiKey))
        {
            throw new ArgumentException("Integration encrypted API key is required for Confluence API calls.", nameof(integration.EncryptedApiKey));
        }

        var httpClient = CreateAndConfigureHttpClient(integration);
        
        // Detect type from URL: Cloud = *.atlassian.net, otherwise OnPremise
        var confluenceType = IntegrationTypeDetector.DetectConfluenceType(integration.Url);

        return confluenceType switch
        {
            ConfluenceType.Cloud => new ConfluenceCloudApiClient(
                httpClient,
                _loggerFactory.CreateLogger<ConfluenceCloudApiClient>()),

            ConfluenceType.OnPremise => new ConfluenceOnPremiseApiClient(
                httpClient,
                _loggerFactory.CreateLogger<ConfluenceOnPremiseApiClient>()),

            _ => throw new InvalidOperationException(
                $"Unsupported ConfluenceType: {confluenceType}")
        };
    }

    /// <summary>
    /// Configures the HTTP client with base URL and authentication headers.
    /// </summary>
    private HttpClient CreateAndConfigureHttpClient(Integration integration)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();

            // Set base address
            try
            {
                client.BaseAddress = new Uri(integration.Url);
            }
            catch (UriFormatException ex)
            {
                throw new ArgumentException(
                    $"Invalid URL format for Confluence integration '{integration.Name}': '{integration.Url}'",
                    nameof(integration.Url),
                    ex);
            }

            // Configure authentication based on URL-detected type
            var apiKey = _credentialEncryptionService.Decrypt(integration.EncryptedApiKey);
            var confluenceType = IntegrationTypeDetector.DetectConfluenceType(integration.Url);

            if (confluenceType == ConfluenceType.Cloud)
            {
                // Cloud: Basic Auth header (email:apiToken)
                var authValue = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{integration.Username}:{apiKey}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            }
            else // OnPremise
            {
                // On-Premise: Bearer Auth header (Personal Access Token)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            return client;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException(
                $"Failed to configure HttpClient for Confluence integration '{integration.Name}'",
                ex);
        }
    }
}
