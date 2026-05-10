using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Domain.Utilities;
using Orchestra.Domain.Validators;
using System.Net.Http.Headers;
using System.Text;

namespace Orchestra.Application.Integrations.Services;

public class IntegrationService : IIntegrationService
{
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly IMcpToolDiscoveryService _mcpToolDiscoveryService;

    public IntegrationService(
        IIntegrationDataAccess integrationDataAccess,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        ICredentialEncryptionService credentialEncryptionService,
        IMcpToolDiscoveryService mcpToolDiscoveryService)
    {
        _integrationDataAccess = integrationDataAccess;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _credentialEncryptionService = credentialEncryptionService;
        _mcpToolDiscoveryService = mcpToolDiscoveryService;
    }

    public async Task<List<IntegrationDto>> GetWorkspaceIntegrationsAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.ValidateMembershipAsync(userId, workspaceId, cancellationToken);

        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);

        return integrations
            .Where(i => i.Provider != ProviderType.MCP_GENERIC)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<IntegrationDto> CreateIntegrationAsync(
        Guid userId,
        CreateIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.ValidateMembershipAsync(
            userId,
            request.WorkspaceId,
            cancellationToken);

        if (request.Provider.Equals("MCP_GENERIC", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "MCP servers must be managed through the MCP Server settings",
                nameof(request.Provider));

        // 2. Parse and validate integration types
        if (request.Types == null || request.Types.Length == 0)
        {
            throw new ArgumentException("At least one integration type must be selected.", nameof(request.Types));
        }

        var integrationTypes = new List<IntegrationType>();
        foreach (var typeStr in request.Types)
        {
            if (!Enum.TryParse<IntegrationType>(typeStr, ignoreCase: true, out var parsedType))
            {
                throw new ArgumentException($"Invalid integration type: {typeStr}", nameof(request.Types));
            }
            integrationTypes.Add(parsedType);
        }

        // 3. Parse and validate provider type
        if (!Enum.TryParse<ProviderType>(request.Provider, ignoreCase: true, out var providerType))
        {
            var validProviders = string.Join(", ", Enum.GetNames<ProviderType>());
            throw new ArgumentException($"Invalid provider: {request.Provider}. Valid providers are: {validProviders}", nameof(request.Provider));
        }

        // 3a. Validate provider-type constraints (provider-driven rules, single authority: ProviderTypeConstraints)
        var allowedTypes = ProviderTypeConstraints.GetAllowedTypes(providerType);
        var invalidTypes = integrationTypes.Where(t => !allowedTypes.Contains(t)).ToList();
        if (invalidTypes.Any())
        {
            throw new InvalidIntegrationTypeForProviderException(
                providerType.ToString(),
                integrationTypes.Select(t => t.ToString()).ToList(),
                allowedTypes.Select(t => t.ToString()).ToList());
        }

        // 3b. Validate filter query based on provider (provider-driven, not type-gated)
        if (providerType == ProviderType.JIRA)
        {
            FilterQueryValidator.ValidateJiraFilterQuery(request.FilterQuery);
        }
        else if (providerType == ProviderType.CONFLUENCE)
        {
            FilterQueryValidator.ValidateConfluenceFilterQuery(request.FilterQuery);
        }

        // 3c. Validate duplicate name
        var isDuplicate = await _integrationDataAccess.ExistsByNameInWorkspaceAsync(
            request.Name,
            request.WorkspaceId,
            cancellationToken: cancellationToken);

        if (isDuplicate)
        {
            throw new DuplicateIntegrationException(request.Name, request.WorkspaceId);
        }

        // 3d. Validate duplicate provider
        var isProviderDuplicate = await _integrationDataAccess.ExistsByProviderInWorkspaceAsync(
            providerType,
            request.WorkspaceId,
            cancellationToken);

        if (isProviderDuplicate)
        {
            throw new DuplicateProviderIntegrationException(providerType.ToString(), request.WorkspaceId);
        }

        // 4. Encrypt API key
        var encryptedApiKey = string.IsNullOrEmpty(request.ApiKey)
            ? null
            : _credentialEncryptionService.Encrypt(request.ApiKey);

        // 5. Create integration entity using domain factory
        var integration = Integration.Create(
            workspaceId: request.WorkspaceId,
            name: request.Name,
            types: integrationTypes,
            provider: providerType,
            url: request.Url,
            username: request.Username,
            encryptedApiKey: encryptedApiKey,
            filterQuery: request.FilterQuery,
            vectorize: request.Vectorize
        );

        // 5b. Connected status tracking removed — Integration entity no longer carries connection state

        // 7. Persist to database
        await _integrationDataAccess.AddAsync(integration, cancellationToken);

        // 8. Return DTO (without credentials)
        return MapToDto(integration);
    }

    public async Task<IntegrationDto> UpdateIntegrationAsync(
        Guid userId,
        Guid integrationId,
        UpdateIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Retrieve the existing integration
        var integration = await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken);
        if (integration == null)
        {
            throw new IntegrationNotFoundException(integrationId);
        }

        // 2. Verify user has access to the integration's workspace
        await _workspaceAuthorizationService.ValidateMembershipAsync(
            userId,
            integration.WorkspaceId,
            cancellationToken);

        // 3. Parse and validate integration types
        if (request.Types == null || request.Types.Length == 0)
        {
            throw new ArgumentException("At least one integration type must be selected.", nameof(request.Types));
        }

        var integrationTypes = new List<IntegrationType>();
        foreach (var typeStr in request.Types)
        {
            if (!Enum.TryParse<IntegrationType>(typeStr, ignoreCase: true, out var parsedType))
            {
                throw new ArgumentException($"Invalid integration type: {typeStr}", nameof(request.Types));
            }
            integrationTypes.Add(parsedType);
        }

        if (!string.IsNullOrEmpty(request.Provider) &&
            request.Provider.Equals("MCP_GENERIC", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "MCP servers must be managed through the MCP Server settings",
                nameof(request.Provider));

        // 4. Parse provider type if provided
        ProviderType? providerType = null;
        if (!string.IsNullOrEmpty(request.Provider))
        {
            if (!Enum.TryParse<ProviderType>(request.Provider, ignoreCase: true, out var parsedProvider))
            {
                var validProviders = string.Join(", ", Enum.GetNames<ProviderType>());
                throw new ArgumentException($"Invalid provider: {request.Provider}. Valid providers are: {validProviders}", nameof(request.Provider));
            }
            providerType = parsedProvider;
        }

        // 4b. Validate provider-type constraints using the same effective-provider resolution as filter validation
        var effectiveProviderForConstraint = providerType ?? integration.Provider;
        var allowedTypesForConstraint = ProviderTypeConstraints.GetAllowedTypes(effectiveProviderForConstraint);
        var invalidTypesForConstraint = integrationTypes.Where(t => !allowedTypesForConstraint.Contains(t)).ToList();
        if (invalidTypesForConstraint.Any())
        {
            throw new InvalidIntegrationTypeForProviderException(
                effectiveProviderForConstraint.ToString(),
                integrationTypes.Select(t => t.ToString()).ToList(),
                allowedTypesForConstraint.Select(t => t.ToString()).ToList());
        }

        // 4a. Validate filter query based on effective provider (provider-driven, not type-gated).
        // Jira always triggers JQL validation; Confluence always triggers CQL validation.
        // GitHub and GitLab bypass structural filter validation entirely.
        var effectiveProvider = providerType ?? integration.Provider;
        if (effectiveProvider == ProviderType.JIRA)
        {
            FilterQueryValidator.ValidateJiraFilterQuery(request.FilterQuery);
        }
        else if (effectiveProvider == ProviderType.CONFLUENCE)
        {
            FilterQueryValidator.ValidateConfluenceFilterQuery(request.FilterQuery);
        }

        var isDuplicate = await _integrationDataAccess.ExistsByNameInWorkspaceAsync(
            request.Name,
            integration.WorkspaceId,
            excludeIntegrationId: integrationId,
            cancellationToken: cancellationToken);

        if (isDuplicate)
        {
            throw new DuplicateIntegrationException(request.Name, integration.WorkspaceId);
        }

        // [FR-02] Conditional provider uniqueness check.
        // Only performed when the submitted provider value differs from the stored provider.
        // Self-exclusion ensures the integration being updated is never counted as its own conflict.
        if (providerType.HasValue && providerType.Value != integration.Provider)
        {
            var isProviderDuplicate = await _integrationDataAccess.ExistsByProviderInWorkspaceExcludingSelf(
                providerType.Value,
                integration.WorkspaceId,
                integrationId,
                cancellationToken);

            if (isProviderDuplicate)
            {
                throw new DuplicateProviderIntegrationException(providerType.Value.ToString(), integration.WorkspaceId);
            }
        }

        // 5. Handle API key: preserve existing if masked, otherwise encrypt new value
        string? encryptedApiKey = null;
        if (!string.IsNullOrEmpty(request.ApiKey) && request.ApiKey != "••••••••••••")
        {
            encryptedApiKey = _credentialEncryptionService.Encrypt(request.ApiKey);
        }

        // 6. Update the integration using domain method
        integration.Update(
            name: request.Name,
            integrationTypes: integrationTypes,
            provider: providerType,
            url: request.Url,
            username: request.Username,
            encryptedApiKey: encryptedApiKey,
            filterQuery: request.FilterQuery,
            vectorize: request.Vectorize
        );

        // 7. Persist changes to database
        await _integrationDataAccess.UpdateAsync(integration, cancellationToken);

        // 8. Return updated DTO (without credentials)
        return MapToDto(integration);
    }

    public async Task<DeleteIntegrationResult> DeleteIntegrationAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        var integration = await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken)
            ?? throw new IntegrationNotFoundException(integrationId);

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, integration.WorkspaceId, cancellationToken);

        integration.Deactivate();
        await _integrationDataAccess.UpdateAsync(integration, cancellationToken);
        return new DeleteIntegrationResult(0, 0, 0);
    }

    public async Task<DeletionImpactDto> GetDeletionImpactAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        var integration = await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken)
            ?? throw new IntegrationNotFoundException(integrationId);

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, integration.WorkspaceId, cancellationToken);

        return new DeletionImpactDto(0, 0, false);
    }

    public async Task ValidateConnectionAsync(
        ValidateIntegrationConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate provider type
        if (!Enum.TryParse<ProviderType>(request.Provider, ignoreCase: true, out var providerType))
        {
            var validProviders = string.Join(", ", Enum.GetNames<ProviderType>());
            throw new ArgumentException($"Invalid provider: {request.Provider}. Valid providers are: {validProviders}", nameof(request.Provider));
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            throw new ArgumentException("URL is required for connection validation.", nameof(request.Url));
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new ArgumentException("API Key is required for connection validation.", nameof(request.ApiKey));
        }

        // Route to provider-specific validator
        switch (providerType)
        {
            case ProviderType.JIRA:
                await ValidateJiraConnectionAsync(request);
                break;
            case ProviderType.CONFLUENCE:
                await ValidateConfluenceConnectionAsync(request);
                break;
            case ProviderType.GITHUB:
                await ValidateGithubConnectionAsync(request);
                break;
            case ProviderType.GITLAB:
                await ValidateGitLabConnectionAsync(request);
                break;
            default:
                throw new ArgumentException($"Connection validation not supported for provider: {request.Provider}", nameof(request.Provider));
        }
    }

    private async Task ValidateGithubConnectionAsync(ValidateIntegrationConnectionRequest request)
    {
        try
        {
            // Validate URL format
            if (string.IsNullOrEmpty(request.Url))
                throw new ArgumentException("GitHub repository URL is required.");

            var uri = new Uri(request.Url);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length < 2)
                throw new ArgumentException("Invalid GitHub repository URL format. Expected: https://github.com/{owner}/{repo}");

            var owner = segments[0];
            var repo = segments[1];

            using var client = new HttpClient();
            client.BaseAddress = new Uri("https://api.github.com");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            client.DefaultRequestHeaders.Add("User-Agent", "Orchestra-GitHub-Integration");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.ApiKey}");

            // Test: get repository details
            var endpoint = $"/repos/{owner}/{repo}";
            var response = await client.GetAsync(endpoint);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException("Failed to authenticate with GitHub. Please verify your token and repository access.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to connect to GitHub: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new InvalidOperationException("Failed to connect to GitHub. Please verify the URL is correct and reachable.");
        }
        catch (UriFormatException)
        {
            throw new ArgumentException("Invalid GitHub URL format.");
        }
    }

    private async Task ValidateGitLabConnectionAsync(ValidateIntegrationConnectionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Url))
                throw new ArgumentException("GitLab project URL is required.");

            var uri = new Uri(request.Url);
            var projectPath = uri.AbsolutePath.Trim('/');

            if (string.IsNullOrEmpty(projectPath) || !projectPath.Contains('/'))
                throw new ArgumentException("Invalid GitLab URL format. Expected: https://gitlab.com/{namespace}/{project}");

            // API base derived from URL — supports both gitlab.com and self-hosted instances
            var apiBaseUrl = $"{uri.Scheme}://{uri.Host}";
            var encodedPath = Uri.EscapeDataString(projectPath);

            using var client = new HttpClient();
            client.BaseAddress = new Uri(apiBaseUrl);
            client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", request.ApiKey);

            var response = await client.GetAsync($"/api/v4/projects/{encodedPath}");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new InvalidOperationException("Failed to authenticate with GitLab. Please verify your token and project access.");

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Failed to connect to GitLab: {response.StatusCode}");
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new InvalidOperationException("Failed to connect to GitLab. Please verify the URL is correct and reachable.");
        }
        catch (UriFormatException)
        {
            throw new ArgumentException("Invalid GitLab URL format.");
        }
    }

    private async Task ValidateJiraConnectionAsync(ValidateIntegrationConnectionRequest request)
    {
        try
        {
            using var client = CreateHttpClient(request);

            // Detect Jira type from URL: Cloud uses API v3, On-Premise uses API v2
            var jiraType = IntegrationTypeDetector.DetectJiraType(request.Url);
            var endpoint = jiraType == JiraType.Cloud
                ? "/rest/api/3/serverInfo"
                : "/rest/api/2/serverInfo";

            var response = await client.GetAsync(endpoint);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("Failed to authenticate with Jira. Please verify your credentials.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to connect to Jira: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new InvalidOperationException("Failed to connect to Jira. Please verify the URL is correct and reachable.");
        }
        catch (UriFormatException)
        {
            throw new ArgumentException("Invalid Jira URL format.");
        }
    }

    private async Task ValidateConfluenceConnectionAsync(ValidateIntegrationConnectionRequest request)
    {
        try
        {
            using var client = CreateHttpClient(request);

            // Detect Confluence type from URL: Cloud has /wiki/ prefix, On-Premise does not
            var confluenceType = IntegrationTypeDetector.DetectConfluenceType(request.Url);
            var endpoint = confluenceType == ConfluenceType.Cloud
                ? "/wiki/rest/api/space?limit=1"
                : "/rest/api/space?limit=1";

            var response = await client.GetAsync(endpoint);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("Failed to authenticate with Confluence. Please verify your credentials.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to connect to Confluence: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new InvalidOperationException("Failed to connect to Confluence. Please verify the URL is correct and reachable.");
        }
        catch (UriFormatException)
        {
            throw new ArgumentException("Invalid Confluence URL format.");
        }
    }

    private HttpClient CreateHttpClient(ValidateIntegrationConnectionRequest request)
    {
        try
        {
            var client = new HttpClient();

            if (string.IsNullOrEmpty(request.Url))
            {
                throw new ArgumentException("Integration URL is required.", nameof(request.Url));
            }

            client.BaseAddress = new Uri(request.Url);

            // Set Basic Auth header
            var authValue = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{request.Username}:{request.ApiKey}"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

            return client;
        }
        catch (UriFormatException ex)
        {
            throw new ArgumentException($"Invalid URL format: '{request.Url}'", nameof(request.Url), ex);
        }
    }

    private static IntegrationDto MapToDto(Integration integration)
    {
        return new IntegrationDto(
            Id: integration.Id.ToString(),
            WorkspaceId: integration.WorkspaceId.ToString(),
            Name: integration.Name,
            Types: integration.Types.Select(t => t.ToString()).ToArray(),
            Icon: integration.Icon,
            Provider: integration.Provider.ToString(),
            Url: integration.Url,
            Username: integration.Username,
            Connected: false,
            LastSync: FormatLastSync(integration.LastSyncAt),
            FilterQuery: integration.FilterQuery,
            Vectorize: integration.Vectorize,
            JiraType: integration.Provider == ProviderType.JIRA
                ? IntegrationTypeDetector.DetectJiraType(integration.Url).ToString()
                : null,
            ConfluenceType: integration.Provider == ProviderType.CONFLUENCE
                ? IntegrationTypeDetector.DetectConfluenceType(integration.Url).ToString()
                : null,
            IsMcpBacked: false,
            McpEndpointUrl: null,
            ToolCount: null,
            McpTransportType: null,
            McpCommand: null
        );
    }

    private static string? FormatLastSync(DateTime? lastSyncAt)
    {
        if (!lastSyncAt.HasValue) return null;

        var timeSpan = DateTime.UtcNow - lastSyncAt.Value;

        if (timeSpan.TotalMinutes < 1) return "Just now";
        if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} min ago";
        if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalDays < 30) return $"{(int)timeSpan.TotalDays}d ago";

        return lastSyncAt.Value.ToString("MMM dd, yyyy");
    }

    public async Task<SyncToolsResultDto> SyncToolsAsync(
        Guid userId,
        Guid integrationId,
        CancellationToken cancellationToken = default)
    {
        var integration = await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken)
            ?? throw new IntegrationNotFoundException(integrationId);

        await _workspaceAuthorizationService.ValidateMembershipAsync(
            userId, integration.WorkspaceId, cancellationToken);

        if (integration.Provider == ProviderType.MCP_GENERIC)
            throw new InvalidOperationException("MCP servers must be managed through the MCP Server settings");

        return await _mcpToolDiscoveryService.SyncToolsAsync(integrationId, cancellationToken);
    }
}
