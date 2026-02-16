using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira;

/// <summary>
/// Factory for creating Jira API client instances based on Jira instance type (Cloud vs On-Premise).
/// Handles HTTP client configuration, authentication, and version-specific routing.
/// </summary>
public class JiraApiClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly ILoggerFactory _loggerFactory;

    public JiraApiClientFactory(
        IHttpClientFactory httpClientFactory,
        ICredentialEncryptionService credentialEncryptionService,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _credentialEncryptionService = credentialEncryptionService;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a Jira API client for the specified integration.
    /// </summary>
    /// <param name="integration">The integration configuration.</param>
    /// <returns>An IJiraApiClient implementation (Cloud or On-Premise).</returns>
    /// <exception cref="ArgumentException">Thrown if URL or API key is missing.</exception>
    /// <exception cref="InvalidOperationException">Thrown if JiraType is not set.</exception>
    public IJiraApiClient CreateClient(Integration integration)
    {
        if (string.IsNullOrEmpty(integration.Url))
        {
            throw new ArgumentException("Integration URL is required for Jira API calls.", nameof(integration.Url));
        }

        if (string.IsNullOrEmpty(integration.EncryptedApiKey))
        {
            throw new ArgumentException("Integration encrypted API key is required for Jira API calls.", nameof(integration.EncryptedApiKey));
        }

        var httpClient = CreateAndConfigureHttpClient(integration);
        
        // Default to Cloud if JiraType is not set (for backward compatibility)
        var jiraType = integration.JiraType ?? JiraType.Cloud;

        return jiraType switch
        {
            JiraType.Cloud => new JiraCloudApiClient(
                httpClient,
                _loggerFactory.CreateLogger<JiraCloudApiClient>()),

            JiraType.OnPremise => new JiraOnPremiseApiClient(
                httpClient,
                _loggerFactory.CreateLogger<JiraOnPremiseApiClient>()),

            _ => throw new InvalidOperationException(
                $"Unsupported JiraType: {jiraType}")
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
                    $"Invalid URL format for Jira integration '{integration.Name}': '{integration.Url}'",
                    nameof(integration.Url),
                    ex);
            }

            // Configure authentication
            var apiKey = _credentialEncryptionService.Decrypt(integration.EncryptedApiKey);

            var jiraType = integration.JiraType ?? JiraType.Cloud;

            if (jiraType == JiraType.Cloud)
            {
                // Basic Auth header: email:apiToken
                var authValue = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{integration.Username}:{apiKey}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            }
            else // OnPremise
            {
                // Bearer Auth header: Personal Access Token
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            return client;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException(
                $"Failed to configure HttpClient for Jira integration '{integration.Name}'",
                ex);
        }
    }
}
