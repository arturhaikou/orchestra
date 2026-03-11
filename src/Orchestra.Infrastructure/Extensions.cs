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
using Orchestra.Infrastructure.Integrations.Providers.GitHub;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;
using Orchestra.Infrastructure.Integrations.Services;
using Microsoft.Extensions.Options;
using Orchestra.Infrastructure.Services;
using Microsoft.Extensions.AI;
using Orchestra.Infrastructure.Tools;
using Orchestra.Infrastructure.Tools.Services;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Configuration;
using System;
using CommunityToolkit.Aspire.OllamaSharp;
using Azure.AI.OpenAI;
using OllamaSharp;

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
        builder.AddAIProvider();
        builder.AddAIModelListService();
        builder.Services.AddScoped<IAIModelRegistry, AIModelRegistry>();
        builder.Services.AddScoped<IChatClientResolver, ChatClientResolver>();
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
        builder.Services.AddScoped<IWorkspaceAIModelValidationService, WorkspaceAIModelValidationService>();
        builder.Services.AddScoped<IAgentService, AgentService>();
        builder.Services.AddSingleton<ICredentialEncryptionService, CredentialEncryptionService>();
        builder.Services.AddScoped<IIntegrationDataAccess, IntegrationDataAccess>();
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
        builder.Services.AddScoped<IToolService, ToolService>();
        builder.Services.AddScoped<IToolCategoryDataAccess, ToolCategoryDataAccess>();
        builder.Services.AddScoped<IAgentToolActionDataAccess, AgentToolActionDataAccess>();
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
        builder.Services.AddScoped<IJiraToolService, JiraToolService>();
        builder.Services.AddScoped<IGitHubToolService, GitHubToolService>();
        builder.Services.AddScoped<IConfluenceToolService, ConfluenceToolService>();
        builder.Services.AddScoped<IInternalToolService, InternalToolService>();
        builder.Services.AddScoped<IGitLabToolService, GitLabToolService>();
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

    /// <summary>
    /// Registers the <see cref="IChatClient"/> for the configured AI provider.
    /// Provider is controlled by the <c>AgentExecution:Provider</c> configuration key
    /// (injected by the AppHost as <c>AgentExecution__Provider</c>).
    ///
    /// Azure path  — uses Aspire.Azure.AI.OpenAI with connection-string key "ai".
    /// Ollama path — uses CommunityToolkit.Aspire.OllamaSharp with connection-string key "ai".
    /// Both paths apply UseFunctionInvocation() and UseOpenTelemetry() middleware.
    ///
    /// Throws <see cref="InvalidOperationException"/> at startup for unknown provider values.
    /// </summary>
    private static IHostApplicationBuilder AddAIProvider(this IHostApplicationBuilder builder)
    {
        var provider = builder.Configuration[
            $"{AgentExecutionSettings.SectionName}:{nameof(AgentExecutionSettings.Provider)}"]
            ?? "Azure";

        var modelName = builder.Configuration[
            $"{AgentExecutionSettings.SectionName}:{nameof(AgentExecutionSettings.ModelDeploymentName)}"]
            ?? "gpt-4o-mini";

        if (provider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddAzureOpenAIClient("ai")
                   .AddChatClient(modelName)
                   .UseFunctionInvocation()
                   .UseOpenTelemetry(configure: c => c.EnableSensitiveData = false);
        }
        else if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            // No model name passed here — Ollama encodes the model in the
            // connection string that was injected by the AppHost via AddModel().
            builder.AddOllamaApiClient("ai")
                   .AddChatClient()
                   .UseFunctionInvocation()
                   .UseOpenTelemetry(configure: c => c.EnableSensitiveData = false);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unrecognized AI provider '{provider}'. " +
                $"Valid values are 'Azure' and 'Ollama'. " +
                $"Check the '{AgentExecutionSettings.SectionName}:" +
                $"{nameof(AgentExecutionSettings.Provider)}' configuration key.");
        }

        return builder;
    }

    private static IHostApplicationBuilder AddAIModelListService(
        this IHostApplicationBuilder builder)
    {
        var provider = builder.Configuration[
            $"{AgentExecutionSettings.SectionName}:{nameof(AgentExecutionSettings.Provider)}"]
            ?? "Azure";

        if (provider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddScoped<IAIModelListService, AzureOpenAIModelListService>();
        }
        else if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddScoped<IAIModelListService, OllamaModelListService>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unrecognized AI provider '{provider}' for IAIModelListService. " +
                $"Valid values are 'Azure' and 'Ollama'. " +
                $"Check the '{AgentExecutionSettings.SectionName}:" +
                $"{nameof(AgentExecutionSettings.Provider)}' configuration key.");
        }

        return builder;
    }

    private static ILogger<T> GetLogger<T>(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<ILogger<T>>();
    }
}