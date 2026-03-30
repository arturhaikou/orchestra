using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Orchestra.Application.CodeReview.Models;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Tools.Services;

namespace Orchestra.Infrastructure.Tools;

/// <summary>
/// Service that retrieves agent tools from the database and converts them
/// into AIFunction instances for Microsoft Agent Framework integration.
/// </summary>
public class ToolRetrieverService : IToolRetrieverService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IToolActionDataAccess _toolActionDataAccess;
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess;
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess;
    private readonly ILogger<ToolRetrieverService> _logger;

    private const string ReviewPullRequestActionName = "review_pull_request";
    private const string ReviewMergeRequestActionName = "review_merge_request";

    /// <summary>
    /// Maps ServiceClassName values from the database to their corresponding DI interface types.
    /// This enables resolving tool service instances from the service provider.
    /// </summary>
    private static readonly Dictionary<string, Type> ServiceTypeMap = new()
    {
        ["Orchestra.Infrastructure.Tools.Services.IJiraToolService"] = typeof(IJiraToolService),
        ["Orchestra.Infrastructure.Tools.Services.IGitHubToolService"] = typeof(IGitHubToolService),
        ["Orchestra.Infrastructure.Tools.Services.IGitLabToolService"] = typeof(IGitLabToolService),
        ["Orchestra.Infrastructure.Tools.Services.IConfluenceToolService"] = typeof(IConfluenceToolService),
        ["Orchestra.Infrastructure.Tools.Services.IInternalToolService"] = typeof(IInternalToolService),
    };

    public ToolRetrieverService(
        IServiceProvider serviceProvider,
        IToolActionDataAccess toolActionDataAccess,
        IToolCategoryDataAccess toolCategoryDataAccess,
        IAgentToolActionDataAccess agentToolActionDataAccess,
        ILogger<ToolRetrieverService> logger)
    {
        _serviceProvider = serviceProvider;
        _toolActionDataAccess = toolActionDataAccess;
        _toolCategoryDataAccess = toolCategoryDataAccess;
        _agentToolActionDataAccess = agentToolActionDataAccess;
        _logger = logger;
    }

    public async Task<IEnumerable<AIFunction>> GetAgentToolsAsync(
        Guid agentId,
        string? modelIdentifier = null,
        string? projectPrinciples = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving tools for agent {AgentId}", agentId);

        // Step 1: Get tool action IDs assigned to the agent
        var toolActionIds = await _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, cancellationToken);

        if (!toolActionIds.Any())
        {
            _logger.LogInformation("No tools assigned to agent {AgentId}", agentId);
            return Enumerable.Empty<AIFunction>();
        }

        // Step 2: Load tool actions (batched retrieval)
        var toolActions = new List<ToolAction>();
        foreach (var id in toolActionIds)
        {
            var action = await _toolActionDataAccess.GetByIdAsync(id, cancellationToken);
            if (action != null)
            {
                toolActions.Add(action);
            }
            else
            {
                _logger.LogWarning("Tool action {ToolActionId} not found in database", id);
            }
        }

        // Step 3: Get unique category IDs and load categories
        var categoryIds = toolActions.Select(a => a.ToolCategoryId).Distinct().ToList();
        var categoryLookup = new Dictionary<Guid, ToolCategory>();

        foreach (var categoryId in categoryIds)
        {
            var category = await _toolCategoryDataAccess.GetByIdAsync(categoryId, cancellationToken);
            if (category != null)
            {
                categoryLookup[categoryId] = category;
            }
            else
            {
                _logger.LogWarning("Tool category {CategoryId} not found in database", categoryId);
            }
        }

        // Step 4: Build AIFunction instances
        var aiFunctions = new List<AIFunction>();

        foreach (var toolAction in toolActions)
        {
            if (!categoryLookup.TryGetValue(toolAction.ToolCategoryId, out var category))
            {
                _logger.LogWarning(
                    "Category not found for tool action {ToolActionId} (CategoryId: {CategoryId})",
                    toolAction.Id, toolAction.ToolCategoryId);
                continue;
            }

            try
            {
                AIFunction? aiFunction;

                if (toolAction.Name is ReviewPullRequestActionName or ReviewMergeRequestActionName)
                {
                    aiFunction = CreateReviewAIFunction(category, toolAction, modelIdentifier, projectPrinciples);
                }
                else
                {
                    aiFunction = CreateAIFunction(category, toolAction);
                }

                if (aiFunction != null)
                {
                    aiFunctions.Add(aiFunction);
                    _logger.LogDebug(
                        "Created AIFunction for tool {ToolName} (Method: {MethodName})",
                        toolAction.Name, toolAction.MethodName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to create AIFunction for tool {ToolName} (Method: {MethodName})",
                    toolAction.Name, toolAction.MethodName);
            }
        }

        _logger.LogInformation(
            "Retrieved {Count} tools for agent {AgentId}",
            aiFunctions.Count, agentId);

        return aiFunctions;
    }

    private AIFunction? CreateAIFunction(ToolCategory category, ToolAction toolAction)
    {
        // Step 1: Resolve service interface type from ServiceTypeMap
        if (!ServiceTypeMap.TryGetValue(category.ServiceClassName, out var serviceInterfaceType))
        {
            _logger.LogWarning(
                "Unknown service class name: {ServiceClassName}. Add to ServiceTypeMap to enable this tool.",
                category.ServiceClassName);
            return null;
        }

        // Step 2: Resolve service instance from DI
        var serviceInstance = _serviceProvider.GetService(serviceInterfaceType);
        if (serviceInstance == null)
        {
            _logger.LogWarning(
                "Failed to resolve service for type: {ServiceType}. Ensure it is registered in DI.",
                serviceInterfaceType.Name);
            return null;
        }

        // Step 3: Get method info via reflection
        var method = serviceInterfaceType.GetMethod(
            toolAction.MethodName,
            BindingFlags.Public | BindingFlags.Instance);

        if (method == null)
        {
            _logger.LogWarning(
                "Method {MethodName} not found on {ServiceType}",
                toolAction.MethodName, serviceInterfaceType.Name);
            return null;
        }

        // Step 4: Create AIFunction using AIFunctionFactory
        try
        {
            return AIFunctionFactory.Create(method, serviceInstance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AIFunctionFactory.Create failed for method {MethodName} on {ServiceType}",
                toolAction.MethodName, serviceInterfaceType.Name);
            return null;
        }
    }

    private AIFunction? CreateReviewAIFunction(
        ToolCategory category,
        ToolAction toolAction,
        string? modelIdentifier,
        string? projectPrinciples)
    {
        if (!ServiceTypeMap.TryGetValue(category.ServiceClassName, out var serviceInterfaceType))
        {
            _logger.LogWarning(
                "Unknown service class name for review action: {ServiceClassName}.",
                category.ServiceClassName);
            return null;
        }

        var serviceInstance = _serviceProvider.GetService(serviceInterfaceType);
        if (serviceInstance == null)
        {
            _logger.LogWarning(
                "Failed to resolve service for type: {ServiceType}.",
                serviceInterfaceType.Name);
            return null;
        }

        // Capture values in local variables so the closure does not capture the
        // mutable parameters by reference.
        var capturedModel = modelIdentifier;
        var capturedPrinciples = projectPrinciples;

        if (serviceInterfaceType == typeof(IGitHubToolService))
        {
            var service = (IGitHubToolService)serviceInstance;

            Func<string, string, string, Task<ReviewToolResult>> closure =
                (workspaceId, integrationId, pullNumber) =>
                    service.ReviewPullRequestAsync(
                        workspaceId, integrationId, pullNumber, capturedModel, capturedPrinciples);

            return AIFunctionFactory.Create(
                closure,
                "review_pull_request",
                "Performs an automated code review of a GitHub pull request, analysing the diff and submitting structured findings.");
        }

        if (serviceInterfaceType == typeof(IGitLabToolService))
        {
            var service = (IGitLabToolService)serviceInstance;

            Func<string, string, string, Task<ReviewToolResult>> closure =
                (workspaceId, integrationId, mrIid) =>
                    service.ReviewMergeRequestAsync(
                        workspaceId, integrationId, mrIid, capturedModel, capturedPrinciples);

            return AIFunctionFactory.Create(
                closure,
                "review_merge_request",
                "Performs an automated code review of a GitLab merge request, analysing the diff and submitting structured findings.");
        }

        _logger.LogWarning(
            "Review action {ActionName} encountered for unsupported service type: {ServiceType}.",
            toolAction.Name, serviceInterfaceType.Name);
        return null;
    }
}