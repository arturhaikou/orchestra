using Bogus;
using Orchestra.Domain.Enums;
using System.Reflection;

namespace Orchestra.Tests.Shared.Builders;

/// <summary>
/// Fluent builder for creating Integration test entities with sensible defaults.
/// </summary>
public class IntegrationBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _workspaceId = Guid.NewGuid();
    private string _name = new Faker().Company.CompanyName();
    private List<IntegrationType> _types = [IntegrationType.TRACKER];
    private ProviderType _provider = ProviderType.JIRA;
    private string? _url;
    private string? _username;
    private string? _encryptedApiKey = "encrypted_key_" + Guid.NewGuid();
    private string? _filterQuery;
    private bool _vectorize = false;
    private bool _isActive = true;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _lastSyncAt = null;

    /// <summary>
    /// Sets the integration ID.
    /// </summary>
    public IntegrationBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the workspace ID.
    /// </summary>
    public IntegrationBuilder WithWorkspaceId(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    /// <summary>
    /// Sets the integration name.
    /// </summary>
    public IntegrationBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the integration types (single type — convenience overload).
    /// </summary>
    public IntegrationBuilder WithType(IntegrationType type)
    {
        _types = [type];
        return this;
    }

    /// <summary>
    /// Sets multiple integration types.
    /// </summary>
    public IntegrationBuilder WithTypes(IEnumerable<IntegrationType> types)
    {
        _types = types.ToList();
        return this;
    }

    /// <summary>
    /// Sets the provider type.
    /// </summary>
    public IntegrationBuilder WithProvider(ProviderType provider)
    {
        _provider = provider;
        return this;
    }

    /// <summary>
    /// Sets the integration URL.
    /// </summary>
    public IntegrationBuilder WithUrl(string? url)
    {
        _url = url;
        return this;
    }

    /// <summary>
    /// Sets the username.
    /// </summary>
    public IntegrationBuilder WithUsername(string? username)
    {
        _username = username;
        return this;
    }

    /// <summary>
    /// Sets the encrypted API key.
    /// </summary>
    public IntegrationBuilder WithEncryptedApiKey(string? apiKey)
    {
        _encryptedApiKey = apiKey;
        return this;
    }

    /// <summary>
    /// Sets the filter query.
    /// </summary>
    public IntegrationBuilder WithFilterQuery(string? filterQuery)
    {
        _filterQuery = filterQuery;
        return this;
    }

    /// <summary>
    /// Sets whether vectorization is enabled.
    /// </summary>
    public IntegrationBuilder WithVectorize(bool vectorize)
    {
        _vectorize = vectorize;
        return this;
    }

    /// <summary>
    /// Sets the IsActive flag on the built integration via reflection (private setter).
    /// </summary>
    public IntegrationBuilder WithIsActive(bool isActive)
    {
        _isActive = isActive;
        return this;
    }

    /// <summary>
    /// Sets the CreatedAt timestamp on the built integration via reflection (private setter).
    /// Use for testing sort-order scenarios.
    /// </summary>
    public IntegrationBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    /// <summary>
    /// Sets the LastSyncAt timestamp on the built integration via reflection (private setter).
    /// Null = "Unverified" status; non-null with Connected=false = "ConnectionFailed" status.
    /// </summary>
    public IntegrationBuilder WithLastSyncAt(DateTime? lastSyncAt)
    {
        _lastSyncAt = lastSyncAt;
        return this;
    }

    /// <summary>
    /// Builds the Integration entity.
    /// </summary>
    public Integration Build()
    {
        var integration = Integration.Create(
            _workspaceId,
            _name,
            _types,
            _provider,
            _url,
            _username,
            _encryptedApiKey,
            _filterQuery,
            _vectorize);

        typeof(Integration)
            .GetProperty(nameof(Integration.Id))!
            .SetValue(integration, _id);

        typeof(Integration)
            .GetProperty(nameof(Integration.IsActive))!
            .SetValue(integration, _isActive);

        typeof(Integration)
            .GetProperty(nameof(Integration.CreatedAt))!
            .SetValue(integration, _createdAt);

        if (_lastSyncAt.HasValue)
        {
            typeof(Integration)
                .GetProperty(nameof(Integration.LastSyncAt))!
                .SetValue(integration, _lastSyncAt);
        }

        return integration;
    }

    public IntegrationBuilder AsMcpBacked(
        string url = "https://mcp.example.com",
        string authType = "API_KEY",
        string? encryptedApiKey = null)
    {
        _provider = ProviderType.MCP_GENERIC;
        _url = url;
        if (encryptedApiKey is not null)
            _encryptedApiKey = encryptedApiKey;
        return this;
    }

    public IntegrationBuilder WithIsMcpBacked(bool isMcpBacked)
    {
        if (isMcpBacked)
            _provider = ProviderType.MCP_GENERIC;
        return this;
    }

    /// <summary>
    /// Marks the integration as MCP-backed (alias for WithIsMcpBacked(true)).
    /// Transport type is now on McpServer; this just sets the provider.
    /// </summary>
    public IntegrationBuilder WithMcpBacked(bool isMcpBacked) => WithIsMcpBacked(isMcpBacked);

    /// <summary>
    /// Sets the MCP endpoint URL. Maps to the integration URL field.
    /// </summary>
    public IntegrationBuilder WithMcpEndpointUrl(string url)
    {
        _url = url;
        return this;
    }

    /// <summary>
    /// Marks the integration as stdio MCP-backed.
    /// Transport type is now on McpServer; this just sets the provider.
    /// </summary>
    public IntegrationBuilder AsStdioMcpBacked()
    {
        _provider = ProviderType.MCP_GENERIC;
        return this;
    }

    /// <summary>
    /// Marks the integration as stdio MCP-backed with a command hint.
    /// Command is now stored on McpServer; this just sets the provider.
    /// </summary>
    public IntegrationBuilder AsStdioMcpBacked(string command)
    {
        _provider = ProviderType.MCP_GENERIC;
        return this;
    }

    /// <summary>
    /// No-op stub — auth type has moved to McpServer entity.
    /// </summary>
    public IntegrationBuilder WithMcpAuthType(McpAuthType authType) => this;

    /// <summary>
    /// No-op stub — command has moved to McpServer entity.
    /// </summary>
    public IntegrationBuilder WithMcpCommand(string command) => this;

    /// <summary>
    /// No-op stub — arguments JSON has moved to McpServer entity.
    /// </summary>
    public IntegrationBuilder WithMcpArgumentsJson(string? argumentsJson) => this;

    /// <summary>
    /// No-op stub — encrypted environment variables have moved to McpServer entity.
    /// </summary>
    public IntegrationBuilder WithMcpEncryptedEnvironmentVariables(string? encryptedEnvVars) => this;

    /// <summary>
    /// No-op stub — transport type has moved to McpServer entity.
    /// </summary>
    public IntegrationBuilder WithMcpTransportType(McpTransportType transportType) => this;

    /// <summary>
    /// Sets the connected/active state. Maps to IsActive.
    /// </summary>
    public IntegrationBuilder AsConnected(bool connected)
    {
        _isActive = connected;
        return this;
    }

    /// <summary>
    /// No-op stub — HTTP transport is now configured on McpServer entity.
    /// </summary>
    public IntegrationBuilder WithHttpTransport() => this;

    /// <summary>
    /// No-op stub — stdio transport is now configured on McpServer entity.
    /// </summary>
    public IntegrationBuilder WithStdioTransport() => this;

    /// <summary>
    /// Creates a Jira Cloud integration.
    /// </summary>
    public static Integration JiraCloudIntegration()
    {
        return new IntegrationBuilder()
            .WithProvider(ProviderType.JIRA)
            .WithType(IntegrationType.TRACKER)
            .WithUrl("https://mycompany.atlassian.net")
            .Build();
    }

    /// <summary>
    /// Creates a Jira self-hosted integration.
    /// </summary>
    public static Integration JiraSelfHostedIntegration()
    {
        return new IntegrationBuilder()
            .WithProvider(ProviderType.JIRA)
            .WithType(IntegrationType.TRACKER)
            .WithUrl("https://jira.mycompany.local")
            .Build();
    }

    /// <summary>
    /// Creates a GitHub integration.
    /// </summary>
    public static Integration GitHubIntegration()
    {
        return new IntegrationBuilder()
            .WithProvider(ProviderType.GITHUB)
            .WithType(IntegrationType.CODE_SOURCE)
            .WithUrl("https://github.com/myorg/myrepo")
            .Build();
    }

    /// <summary>
    /// Creates a GitLab integration (supports both gitlab.com and self-hosted).
    /// </summary>
    public static Integration GitLabIntegration()
    {
        return new IntegrationBuilder()
            .WithProvider(ProviderType.GITLAB)
            .WithType(IntegrationType.CODE_SOURCE)
            .WithUrl("https://gitlab.com/myorg/myrepo")
            .Build();
    }

    /// <summary>
    /// Creates a Confluence integration.
    /// </summary>
    public static Integration ConfluenceIntegration()
    {
        return new IntegrationBuilder()
            .WithProvider(ProviderType.CONFLUENCE)
            .WithType(IntegrationType.KNOWLEDGE_BASE)
            .WithUrl("https://mycompany.atlassian.net/wiki")
            .Build();
    }

    /// <summary>
    /// Creates a disconnected integration (legacy stub — connection tracking removed).
    /// </summary>
    public static Integration DisconnectedIntegration()
    {
        return new IntegrationBuilder()
            .Build();
    }
}
