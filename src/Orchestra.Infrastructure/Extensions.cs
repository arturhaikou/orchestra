using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using Orchestra.Application.CodeReview;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Auth.Services;
using Orchestra.Application.Integrations.Services;
using Orchestra.Application.Workspaces.Services;
using Orchestra.Application.Tickets.Services;
using Orchestra.Application.Jobs.Services;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Tools.Services;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Application.Workflows.Services;
using Orchestra.Domain.Interfaces;
using Orchestra.Infrastructure.CodeReview;
using Orchestra.Infrastructure.Integrations;
using Orchestra.Infrastructure.Agents;
using Orchestra.Infrastructure.Jobs;
using Orchestra.Infrastructure.Persistence;
using Orchestra.Infrastructure.Repositories;
using Orchestra.Infrastructure.Security;
using Orchestra.Infrastructure.Integrations.Providers;
using Orchestra.Infrastructure.Integrations.Providers.Jira;
using Orchestra.Infrastructure.Integrations.Providers.GitHub;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;
using Orchestra.Infrastructure.Integrations.Services;
using Orchestra.Infrastructure.Hubs;
using Orchestra.Infrastructure.Workflows;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Orchestra.Infrastructure.Services;
using Orchestra.Infrastructure.Tools;
using Orchestra.Infrastructure.Tools.Services;
using Orchestra.Infrastructure.Mcp;
using Orchestra.Infrastructure.McpServers;
using Orchestra.Application.McpServers;
using Orchestra.Application.McpServers.Interfaces;
using McpServerQueryServiceNew = Orchestra.Application.McpServers.McpServerQueryService;
using IMcpServerQueryServiceNew = Orchestra.Application.McpServers.Interfaces.IMcpServerQueryService;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Configuration;

public static class Extensions
{
    /// <summary>
    /// Registers all infrastructure services including data access, security, integrations, and ADF conversion.
    /// Requires Aspire service reference for "adfgenerator" to enable ADF-to-Markdown conversion.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for method chaining.</returns>
    public static IHostApplicationBuilder AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<AppDbContext>("Orchestra");
        builder.Services.AddScoped<IDatabaseMigrator, EfCoreDatabaseMigrator>();
        builder.Services.AddDataProtection()
            .SetApplicationName("Orchestra")
            .PersistKeysToDbContext<AppDbContext>();

        // Register a no-op hub context only if SignalR infrastructure isn't present.
        // The API will call AddSignalR() which registers the real hub context.
        // The Worker doesn't call AddSignalR(), so it gets the no-op version.
        var hasSignalRServices = builder.Services.Any(sd =>
            sd.ServiceType.Name.Contains("SignalR") ||
            sd.ServiceType.Name.Contains("HubContext") ||
            sd.ServiceType.Name.Contains("HubConnectionManager"));

        if (!hasSignalRServices && !builder.Services.Any(sd => sd.ServiceType == typeof(IHubContext<NotificationHub>)))
        {
            builder.Services.AddScoped<IHubContext<NotificationHub>>(provider =>
                new NoOpHubContext<NotificationHub>());
        }

        builder.Services.AddScoped<IChatClientResolver, ChatClientResolver>();
        // AI Provider Phase 2 — concrete EF Core repository implementations
        builder.Services.AddScoped<IWorkspaceAIProviderRepository, EfWorkspaceAIProviderRepository>();
        builder.Services.AddScoped<IProviderCredentialEncryptionService, DataProtectionCredentialEncryptionService>();
        builder.Services.AddScoped<IAIProviderResolver, AIProviderResolver>();
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<IAzureOpenAIModelDiscoveryService, AzureOpenAIModelDiscoveryService>();
        builder.Services.AddSingleton<IAzureOpenAILimitsService, AzureOpenAILimitsService>();
        builder.Services.AddScoped<IOllamaModelDiscoveryService, OllamaModelDiscoveryService>();
        builder.Services.AddScoped<IWorkspaceProviderService, WorkspaceProviderService>();
        // Bind ADF conversion service configuration from appsettings.json
        builder.Services.Configure<AdfConversionServiceOptions>(
            builder.Configuration.GetSection("AdfConversionService"));

        builder.Services.AddScoped<IPasswordHashingService, PasswordHashingService>();
        builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
        builder.Services.AddScoped<IUserDataAccess, UserDataAccess>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IWorkspaceDataAccess, WorkspaceDataAccess>();
        builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
        builder.Services.AddScoped<IWorkspaceAuthorizationService, WorkspaceAuthorizationService>();
        builder.Services.AddScoped<IAgentService, AgentService>();
        builder.Services.AddSingleton<ICredentialEncryptionService, CredentialEncryptionService>();
        builder.Services.AddSingleton<IBuiltInAgentTemplateRegistry, BuiltInAgentTemplateRegistry>();
        builder.Services.AddSingleton<ITemplateRegistry, TemplateRegistry>();
        builder.Services.AddScoped<ITemplateAvailabilityResolver, TemplateAvailabilityResolver>();
        builder.Services.AddScoped<IIntegrationDataAccess, IntegrationDataAccess>();
        builder.Services.AddScoped<IMcpServerDataAccess, McpServerDataAccess>();
        builder.Services.AddScoped<IMcpServerService, McpServerService>();
        builder.Services.AddScoped<IIntegrationResolver, IntegrationResolver>();
        builder.Services.AddScoped<IIntegrationService, IntegrationService>();
        builder.Services.AddScoped<IAgentDataAccess, AgentDataAccess>();
        builder.Services.AddScoped<ITicketDataAccess, TicketDataAccess>();
        builder.Services.AddScoped<ITicketIdParsingService, TicketIdParsingService>();
        // builder.Services.AddScoped<ITicketLookupCacheService, TicketLookupCacheService>();
        builder.Services.AddScoped<ITicketAssignmentValidationService, TicketAssignmentValidationService>();
        builder.Services.AddScoped<ITicketAuthorizationService, TicketAuthorizationService>();
        builder.Services.AddScoped<ITicketCommentService, TicketCommentService>();
        builder.Services.AddScoped<ITicketEnrichmentService, TicketEnrichmentService>();
        builder.Services.AddScoped<ITicketPaginationService, TicketPaginationService>();
        builder.Services.AddScoped<ITicketQueryService, TicketQueryService>();
        builder.Services.AddScoped<ITicketCommandService, TicketCommandService>();
        builder.Services.AddScoped<IExternalTicketFetchingService, TicketExternalFetchingService>();
        builder.Services.AddScoped<ITicketMaterializationService, TicketMaterializationService>();
        builder.Services.AddScoped<ITicketService, TicketService>();
        builder.Services.AddScoped<IJobDataAccess, JobDataAccess>();
        builder.Services.AddScoped<IJobStepWriter, JobStepWriter>();
        builder.Services.AddScoped<IJobService, JobService>();
        builder.Services.AddScoped<IAgentQuestionRepository, AgentQuestionRepository>();
        builder.Services.AddScoped<IConversationSnapshotRepository, ConversationSnapshotRepository>();
        builder.Services.AddScoped<IToolService, ToolService>();
        builder.Services.AddScoped<IToolCategoryDataAccess, ToolCategoryDataAccess>();
        builder.Services.AddScoped<IAgentToolActionDataAccess, AgentToolActionDataAccess>();
        builder.Services.AddScoped<IAgentSubAgentDataAccess, AgentSubAgentDataAccess>();
        builder.Services.AddScoped<IAgentSubAgentAssignmentService, AgentSubAgentAssignmentService>();
        builder.Services.AddScoped<IToolActionDataAccess, ToolActionDataAccess>();

        // Ticket Provider Infrastructure
        // HttpClient factory for provider HTTP calls (Jira, Azure DevOps, etc.)
        builder.Services.AddHttpClient();

        // Named HttpClient for Jira On-Premise:
        // - AllowAutoRedirect=false so that the SSO/auth-plugin 302 redirect is NOT silently
        //   followed. Without this the server returns a 200 HTML login page which then causes
        //   a JsonException instead of a meaningful auth error.
        // - Accept: application/json so Jira returns JSON error bodies instead of HTML error pages.
        builder.Services.AddHttpClient("JiraOnPremise", client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        // ADF Conversion Service
        // Converts Atlassian Document Format (ADF) to Markdown
        // Uses Aspire service discovery and Standard Resilience Handler (retry/circuit breaker)
        builder.Services.AddScoped<IAdfConversionService, AdfConversionService>();

        builder.Services.AddHttpClient("adfgenerator", client =>
        {
            client.BaseAddress = new Uri("http://adfgenerator");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Summarization Service
        // Generates AI-powered summaries for ticket content
        builder.Services.AddScoped<ISummarizationService, SummarizationService>();

        // Sentiment Analysis Service
        // Analyzes sentiment of ticket comments using external AI service
        builder.Services.AddScoped<ISentimentAnalysisService, SentimentAnalysisService>();

        builder.Services.AddScoped<ITicketProviderFactory, TicketProviderFactory>();

        builder.Services.AddSingleton<ITicketMappingService, TicketMappingService>();

        // Jira API client abstraction for version support (Cloud v3, On-Premise v2)
        builder.Services.AddScoped<JiraApiClientFactory>();
        builder.Services.AddScoped<IJiraTextContentConverter, JiraTextContentConverter>();

        // GitHub API client abstraction
        builder.Services.AddScoped<IGitHubApiClientFactory, GitHubApiClientFactory>();

        // GitLab API client abstraction
        builder.Services.AddScoped<IGitLabApiClientFactory, GitLabApiClientFactory>();

        // Confluence API client abstraction for version support (Cloud v3, On-Premise v2)
        builder.Services.AddScoped<Orchestra.Infrastructure.Integrations.Providers.Confluence.ConfluenceApiClientFactory>();

        // Ticket provider implementations
        builder.Services.AddScoped<JiraTicketProvider>();
        builder.Services.AddScoped<GitHubTicketProvider>();
        builder.Services.AddScoped<GitLabTicketProvider>();

        // Tool Services
        builder.Services.AddScoped<ICodeReviewPipeline, HybridReviewPipeline>();
        builder.Services.AddScoped<ICodeReviewProviderFactory, CodeReviewProviderFactory>();
        builder.Services.AddScoped<ISignatureChangeDetector, RegexSignatureChangeDetector>();
        builder.Services.AddScoped<IJiraToolService, JiraToolService>();
        builder.Services.AddScoped<IGitHubToolService, GitHubToolService>();
        builder.Services.AddScoped<IConfluenceToolService, ConfluenceToolService>();
        builder.Services.AddScoped<IInternalToolService, InternalToolService>();
        builder.Services.AddScoped<IGitLabToolService, GitLabToolService>();
        builder.Services.AddScoped<IToolScanningService, ToolScanningService>();
        builder.Services.AddScoped<IToolRetrieverService, ToolRetrieverService>();
        builder.Services.AddScoped<IToolValidationService, ToolValidationService>();

        // MCP infrastructure
        builder.Services.AddScoped<IMcpClientFactory, McpClientFactory>();
        builder.Services.AddScoped<IMcpToolDiscoveryService, McpToolDiscoveryService>();
        builder.Services.AddScoped<IMcpToolSeedingService, McpToolSeedingService>();
        builder.Services.AddScoped<IMcpServerConnectionService, McpServerConnectionService>();
        builder.Services.AddScoped<IMcpServerConnectionChecker, McpServerConnectionChecker>();
        builder.Services.AddScoped<IMcpServerQueryServiceNew, McpServerQueryServiceNew>();
        builder.Services.AddScoped<IAgentMcpToolDataAccess, AgentMcpToolDataAccess>();
        builder.Services.AddScoped<IAgentToolAssignmentService, AgentToolAssignmentService>();
        builder.Services.AddScoped<IAgentOptionalToolService, AgentOptionalToolService>();
        builder.Services.AddScoped<IMcpServerImpactCounter, McpServerImpactCounter>();
        builder.Services.AddScoped<IMcpServerCommandService, McpServerCommandService>();
        builder.Services.AddScoped<IMcpServerToolFetcher, McpServerToolFetcher>();

        // AI CLI integrations
        builder.Services.AddScoped<Orchestra.Application.AiCliIntegrations.Interfaces.IAiCliIntegrationDataAccess, Orchestra.Infrastructure.AiCliIntegrations.AiCliIntegrationDataAccess>();
        builder.Services.AddScoped<Orchestra.Application.AiCliIntegrations.Interfaces.IAiCliIntegrationCommandService, Orchestra.Application.AiCliIntegrations.AiCliIntegrationCommandService>();
        builder.Services.AddScoped<Orchestra.Application.AiCliIntegrations.Interfaces.IAiCliIntegrationQueryService, Orchestra.Application.AiCliIntegrations.AiCliIntegrationQueryService>();
        builder.Services.AddScoped<Orchestra.Infrastructure.AiCliIntegrations.IAiCliClientFactory, Orchestra.Infrastructure.AiCliIntegrations.AiCliClientFactory>();
        builder.Services.AddScoped<Orchestra.Application.AiCliIntegrations.Interfaces.ICopilotModelDiscoveryService, Orchestra.Infrastructure.AiCliIntegrations.CopilotModelDiscoveryService>();

        // Agent Execution Services
        builder.Services.Configure<AgentExecutionSettings>(
            builder.Configuration.GetSection(AgentExecutionSettings.SectionName));
        builder.Services.AddScoped<IAgentRuntimeService, AgentRuntimeService>();
        builder.Services.AddScoped<IChatAgentRunner, ChatAgentRunner>();
        builder.Services.AddScoped<IAgentOrchestrationService, AgentOrchestrationService>();
        builder.Services.AddScoped<IAgentContextBuilder, AgentContextBuilder>();
        builder.Services.AddScoped<ITicketAgentExecutionDataAccess, TicketAgentExecutionDataAccess>();
        builder.Services.AddScoped<INotificationService, NotificationService>();

        // Workflow Services
        builder.Services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
        builder.Services.AddScoped<IWorkflowExecutionRepository, WorkflowExecutionRepository>();
        builder.Services.AddScoped<IWorkflowDefinitionService, WorkflowDefinitionService>();
        builder.Services.AddScoped<IWorkflowExecutionService, WorkflowExecutionService>();
        builder.Services.AddScoped<IWorkflowExecutionEngine, WorkflowExecutionEngine>();

        // AG-UI streaming endpoint services
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IAgentAGUIBuildService, AgentAGUIBuildService>();
        builder.Services.AddSingleton<DynamicWorkspaceAgent>();

        // Skills
        builder.Services.AddScoped<ISkillDataAccess, Orchestra.Infrastructure.Skills.SkillDataAccess>();
        builder.Services.AddScoped<IAgentSkillDataAccess, Orchestra.Infrastructure.Skills.AgentSkillDataAccess>();
        builder.Services.AddScoped<Orchestra.Application.Skills.Services.ISkillService, Orchestra.Application.Skills.Services.SkillService>();
        builder.Services.AddScoped<ISkillFolderDataAccess, Orchestra.Infrastructure.Skills.SkillFolderDataAccess>();
        builder.Services.AddScoped<IAgentSkillFolderDataAccess, Orchestra.Infrastructure.Skills.AgentSkillFolderDataAccess>();
        builder.Services.AddScoped<ISkillFolderDiscoveryService, Orchestra.Infrastructure.Skills.SkillFolderDiscoveryService>();
        builder.Services.AddScoped<Orchestra.Application.Skills.Services.ISkillFolderService, Orchestra.Application.Skills.Services.SkillFolderService>();

        return builder;
    }




    private static ILogger<T> GetLogger<T>(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<ILogger<T>>();
    }
}