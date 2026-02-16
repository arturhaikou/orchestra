using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Auth.Services;
using Orchestra.Application.Integrations.Services;
using Orchestra.Application.Workspaces.Services;
using Orchestra.Application.Tickets.Services;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Tools.Services;
using Orchestra.Domain.Interfaces;
using Orchestra.Infrastructure.Integrations;
using Orchestra.Infrastructure.Agents;
using Orchestra.Infrastructure.Persistence;
using Orchestra.Infrastructure.Security;
using Orchestra.Infrastructure.Integrations.Providers;
using Orchestra.Infrastructure.Integrations.Providers.Jira;
using Orchestra.Infrastructure.Integrations.Services;
using Microsoft.Extensions.Options;
using Orchestra.Infrastructure.Services;
using Microsoft.Extensions.AI;
using Orchestra.Infrastructure.Tools;
using Orchestra.Infrastructure.Tools.Services;
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
        builder.AddAzureOpenAI();
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
        builder.Services.AddScoped<IIntegrationDataAccess, IntegrationDataAccess>();
        builder.Services.AddScoped<IIntegrationService, IntegrationService>();
        builder.Services.AddScoped<IAgentDataAccess, AgentDataAccess>();
        builder.Services.AddScoped<ITicketDataAccess, TicketDataAccess>();
        builder.Services.AddScoped<ITicketService, TicketService>();
        builder.Services.AddScoped<IToolService, ToolService>();
        builder.Services.AddScoped<IToolCategoryDataAccess, ToolCategoryDataAccess>();
        builder.Services.AddScoped<IAgentToolActionDataAccess, AgentToolActionDataAccess>();
        builder.Services.AddScoped<IToolActionDataAccess, ToolActionDataAccess>();

        // Ticket Provider Infrastructure
        // HttpClient factory for provider HTTP calls (Jira, Azure DevOps, etc.)
        builder.Services.AddHttpClient();

        // ADF Conversion Service
        // Converts Atlassian Document Format (ADF) to Markdown
        // Uses Aspire service discovery and Standard Resilience Handler (retry/circuit breaker)
        builder.Services.AddScoped<IAdfConversionService, AdfConversionService>();

        builder.Services.AddHttpClient("adfgenerator", client =>
        {
            client.BaseAddress = new Uri("http://adfgenerator");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Configure named HTTP client for adfgenerator service with timeout
        //builder.Services.AddHttpClient("adfgenerator", (serviceProvider, client) =>
        //{
        //    var options = serviceProvider.GetRequiredService<IOptions<AdfConversionServiceOptions>>().Value;
        //    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        //})
        //.AddServiceDiscovery();

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

        // Ticket provider implementations
        builder.Services.AddScoped<JiraTicketProvider>();

        // Tool Services
        builder.Services.AddScoped<IJiraToolService, JiraToolService>();
        builder.Services.AddScoped<IGitHubToolService, GitHubToolService>();
        builder.Services.AddScoped<IConfluenceToolService, ConfluenceToolService>();
        builder.Services.AddScoped<IInternalToolService, InternalToolService>();
        builder.Services.AddScoped<IToolScanningService, ToolScanningService>();
        builder.Services.AddScoped<IToolRetrieverService, ToolRetrieverService>();
        builder.Services.AddScoped<IToolValidationService, ToolValidationService>();

        // Agent Execution Services
        builder.Services.Configure<AgentExecutionSettings>(
            builder.Configuration.GetSection(AgentExecutionSettings.SectionName));
        builder.Services.AddScoped<IAgentRuntimeService, AgentRuntimeService>();
        builder.Services.AddScoped<IAgentOrchestrationService, AgentOrchestrationService>();
        builder.Services.AddScoped<IAgentContextBuilder, AgentContextBuilder>();
        builder.Services.AddScoped<ITicketAgentExecutionDataAccess, TicketAgentExecutionDataAccess>();

        return builder;
    }

    public static TBuilder AddAzureOpenAI<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Read model deployment name from configuration
        var settings = builder.Configuration
            .GetSection(AgentExecutionSettings.SectionName)
            .Get<AgentExecutionSettings>() ?? new AgentExecutionSettings();
        
        var modelName = settings.ModelDeploymentName;
        var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<IHostApplicationBuilder>>();
        logger.LogInformation("Configuring Azure OpenAI with model: {ModelName}", modelName);

        var openai = builder.AddAzureOpenAIClient("openai");
        openai.AddChatClient(modelName)
            .UseFunctionInvocation()
            .UseOpenTelemetry(configure: c =>
                c.EnableSensitiveData = builder.Environment.IsDevelopment());
        return builder;
    }

    private static ILogger<T> GetLogger<T>(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<ILogger<T>>();
    }
}