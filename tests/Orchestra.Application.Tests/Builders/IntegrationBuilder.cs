using Bogus;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Tests.Builders;

/// <summary>
/// Fluent builder for creating Integration test entities with sensible defaults.
/// </summary>
public class IntegrationBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _workspaceId = Guid.NewGuid();
    private string _name = new Faker().Company.CompanyName();
    private IntegrationType _type = IntegrationType.TRACKER;
    private ProviderType _provider = ProviderType.JIRA;
    private string? _url;
    private string? _username;
    private string? _encryptedApiKey = "encrypted_key_" + Guid.NewGuid();
    private string? _filterQuery;
    private bool _vectorize = false;
    private bool _connected = true;
    private JiraType? _jiraType = JiraType.Cloud;
    private ConfluenceType? _confluenceType = ConfluenceType.Cloud;

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
    /// Sets the integration type.
    /// </summary>
    public IntegrationBuilder WithType(IntegrationType type)
    {
        _type = type;
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
    /// Sets the connection status.
    /// </summary>
    public IntegrationBuilder AsConnected(bool connected)
    {
        _connected = connected;
        return this;
    }

    /// <summary>
    /// Sets the Jira type (Cloud or Self-Hosted).
    /// </summary>
    public IntegrationBuilder WithJiraType(JiraType? jiraType)
    {
        _jiraType = jiraType;
        return this;
    }

    /// <summary>
    /// Sets the Confluence type (Cloud or On-Premise).
    /// </summary>
    public IntegrationBuilder WithConfluenceType(ConfluenceType? confluenceType)
    {
        _confluenceType = confluenceType;
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
            _type,
            _provider,
            _url,
            _username,
            _encryptedApiKey,
            _filterQuery,
            _vectorize,
            _jiraType,
            _confluenceType
        );

        if (!_connected)
        {
            integration.Update(
                _name,
                _provider,
                _url,
                _username,
                _encryptedApiKey,
                _filterQuery,
                _vectorize,
                _jiraType,
                _confluenceType,
                _connected);
        }

        return integration;
    }

    /// <summary>
    /// Creates a Jira Cloud integration.
    /// </summary>
    public static Integration JiraCloudIntegration()
    {
        return new IntegrationBuilder()
            .WithProvider(ProviderType.JIRA)
            .WithType(IntegrationType.TRACKER)
            .WithUrl("https://mycompany.atlassian.net")
            .WithJiraType(JiraType.Cloud)
            .AsConnected(true)
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
            .WithJiraType(JiraType.OnPremise)
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
    /// Creates a disconnected integration.
    /// </summary>
    public static Integration DisconnectedIntegration()
    {
        return new IntegrationBuilder()
            .AsConnected(false)
            .Build();
    }
}
