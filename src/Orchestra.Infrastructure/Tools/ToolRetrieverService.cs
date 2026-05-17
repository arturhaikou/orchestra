using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Microsoft.Agents.AI;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.CodeReview.Models;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.AiCliIntegrations;
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
    private readonly IMcpClientFactory _mcpClientFactory;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IProviderCredentialEncryptionService _encryptionService;
    private readonly IMcpServerDataAccess _mcpServerDataAccess;
    private readonly IAgentMcpToolDataAccess _agentMcpToolDataAccess;
    private readonly IAgentSubAgentDataAccess _agentSubAgentDataAccess;
    private readonly IChatClientResolver _chatClientResolver;
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry;
    private readonly IAiCliClientFactory _cliClientFactory;
    private readonly ILogger<ToolRetrieverService> _logger;

    private const string ReviewPullRequestActionName = "review_pull_request";
    private const string ReviewMergeRequestActionName = "review_merge_request";
    private const int MaxSubAgentDepth = 3;

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
        IMcpClientFactory mcpClientFactory,
        IIntegrationDataAccess integrationDataAccess,
        IAgentDataAccess agentDataAccess,
        IProviderCredentialEncryptionService encryptionService,
        IMcpServerDataAccess mcpServerDataAccess,
        IAgentMcpToolDataAccess agentMcpToolDataAccess,
        IAgentSubAgentDataAccess agentSubAgentDataAccess,
        IChatClientResolver chatClientResolver,
        IBuiltInAgentTemplateRegistry templateRegistry,
        IAiCliClientFactory cliClientFactory,
        ILogger<ToolRetrieverService> logger)
    {
        _serviceProvider = serviceProvider;
        _toolActionDataAccess = toolActionDataAccess;
        _toolCategoryDataAccess = toolCategoryDataAccess;
        _agentToolActionDataAccess = agentToolActionDataAccess;
        _mcpClientFactory = mcpClientFactory;
        _integrationDataAccess = integrationDataAccess;
        _agentDataAccess = agentDataAccess;
        _encryptionService = encryptionService;
        _mcpServerDataAccess = mcpServerDataAccess;
        _agentMcpToolDataAccess = agentMcpToolDataAccess;
        _agentSubAgentDataAccess = agentSubAgentDataAccess;
        _chatClientResolver = chatClientResolver;
        _templateRegistry = templateRegistry;
        _cliClientFactory = cliClientFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<AIFunction>> GetAgentToolsAsync(
        Guid agentId,
        string? modelIdentifier = null,
        string? projectPrinciples = null,
        CancellationToken cancellationToken = default)
    {
        return await GetAgentToolsInternalAsync(agentId, modelIdentifier, projectPrinciples, depth: MaxSubAgentDepth, cancellationToken);
    }

    private async Task<IEnumerable<AIFunction>> GetAgentToolsInternalAsync(
        Guid agentId,
        string? modelIdentifier,
        string? projectPrinciples,
        int depth,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving tools for agent {AgentId} (depth {Depth})", agentId, depth);

        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);
        if (agent is null)
        {
            _logger.LogWarning("Agent {AgentId} not found; returning empty tool list", agentId);
            return [];
        }

        var toolActionIds = await _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, cancellationToken);

        var toolActions = toolActionIds.Count > 0
            ? await LoadToolActionsAsync(toolActionIds, cancellationToken)
            : new List<ToolAction>();

        var nativeActions = toolActions.Where(a => !a.IsMcpTool).ToList();
        var mcpActions = toolActions.Where(a => a.IsMcpTool).ToList();

        var nativeAiFunctions = await ResolveNativeToolsAsync(nativeActions, modelIdentifier, projectPrinciples, cancellationToken);
        var mcpAiFunctions = await ResolveMcpToolsIfAnyAsync(agent.WorkspaceId, mcpActions, cancellationToken);
        var connectedMcpFunctions = await ResolveConnectedMcpToolsAsync(agentId, agent.WorkspaceId, cancellationToken);
        var subAgentFunctions = await ResolveSubAgentToolsAsync(agentId, depth, cancellationToken);

        var allFunctions = nativeAiFunctions
            .Concat(mcpAiFunctions)
            .Concat(connectedMcpFunctions)
            .Concat(subAgentFunctions)
            .ToList();

        _logger.LogInformation("Retrieved {Count} tools for agent {AgentId}", allFunctions.Count, agentId);

        return allFunctions;
    }

    private async Task<List<ToolAction>> LoadToolActionsAsync(
        List<Guid> toolActionIds,
        CancellationToken cancellationToken)
    {
        var toolActions = new List<ToolAction>();

        foreach (var id in toolActionIds)
        {
            var action = await _toolActionDataAccess.GetByIdAsync(id, cancellationToken);
            if (action != null)
                toolActions.Add(action);
            else
                _logger.LogWarning("Tool action {ToolActionId} not found in database", id);
        }

        return toolActions;
    }

    private async Task<List<AIFunction>> ResolveNativeToolsAsync(
        List<ToolAction> nativeActions,
        string? modelIdentifier,
        string? projectPrinciples,
        CancellationToken cancellationToken)
    {
        var categoryIds = nativeActions.Select(a => a.ToolCategoryId).Distinct().ToList();
        var categoryLookup = new Dictionary<Guid, ToolCategory>();

        foreach (var categoryId in categoryIds)
        {
            var category = await _toolCategoryDataAccess.GetByIdAsync(categoryId, cancellationToken);
            if (category != null)
                categoryLookup[categoryId] = category;
            else
                _logger.LogWarning("Tool category {CategoryId} not found in database", categoryId);
        }

        var aiFunctions = new List<AIFunction>();

        foreach (var toolAction in nativeActions)
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
                    aiFunction = CreateReviewAIFunction(category, toolAction, modelIdentifier, projectPrinciples);
                else
                    aiFunction = CreateAIFunction(category, toolAction);

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

        return aiFunctions;
    }

    private async Task<List<AIFunction>> ResolveMcpToolsIfAnyAsync(
        Guid agentWorkspaceId,
        List<ToolAction> mcpActions,
        CancellationToken cancellationToken)
    {
        if (mcpActions.Count == 0)
            return [];

        return await ResolveMcpToolsAsync(mcpActions, agentWorkspaceId, cancellationToken);
    }

    private async Task<List<AIFunction>> ResolveConnectedMcpToolsAsync(
        Guid agentId,
        Guid agentWorkspaceId,
        CancellationToken cancellationToken)
    {
        var connectedTools = await _agentMcpToolDataAccess.GetByAgentIdAsync(agentId, cancellationToken);
        if (connectedTools.Count == 0)
            return [];

        var groups = connectedTools.GroupBy(t => t.McpServerId);

        var tasks = groups.Select(g =>
            ResolveConnectedMcpToolsForServerAsync(g.Key, g.Select(t => t.ToolName).ToList(), agentWorkspaceId, cancellationToken));

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }

    private async Task<List<AIFunction>> ResolveConnectedMcpToolsForServerAsync(
        Guid mcpServerId,
        IReadOnlyList<string> toolNames,
        Guid agentWorkspaceId,
        CancellationToken cancellationToken)
    {
        var mcpServer = await _mcpServerDataAccess.GetByIdAsync(mcpServerId, cancellationToken);
        if (mcpServer is null)
        {
            _logger.LogWarning("Connected MCP server {McpServerId} not found; skipping tools", mcpServerId);
            return [];
        }

        if (mcpServer.WorkspaceId != agentWorkspaceId)
        {
            _logger.LogWarning(
                "Cross-workspace access denied: connected MCP server {McpServerId} belongs to workspace {McpServerWorkspace}, agent is in {AgentWorkspace}",
                mcpServerId, mcpServer.WorkspaceId, agentWorkspaceId);
            return [];
        }

        var mcpClient = await ConnectToMcpServerAsync(mcpServer, cancellationToken);
        if (mcpClient is null)
            return [];

        return await MatchConnectedToolsAsync(mcpClient, toolNames, cancellationToken);
    }

    private async Task<List<AIFunction>> MatchConnectedToolsAsync(
        IMcpClient mcpClient,
        IReadOnlyList<string> toolNames,
        CancellationToken cancellationToken)
    {
        IEnumerable<IMcpToolDescriptor> serverTools;

        try
        {
            serverTools = await mcpClient.ListToolsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list tools from connected MCP server");
            return [];
        }

        var serverToolLookup = serverTools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var aiFunctions = new List<AIFunction>();

        foreach (var toolName in toolNames)
        {
            if (!serverToolLookup.TryGetValue(toolName, out var serverTool))
            {
                _logger.LogWarning("Connected MCP tool '{ToolName}' no longer exists on server; skipping", toolName);
                continue;
            }

            aiFunctions.Add(serverTool.AsAIFunction());
        }

        return aiFunctions;
    }

    private async Task<List<AIFunction>> ResolveSubAgentToolsAsync(
        Guid parentAgentId,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth <= 0)
        {
            _logger.LogWarning(
                "Maximum sub-agent recursion depth reached for agent {AgentId}; skipping further sub-agents",
                parentAgentId);
            return [];
        }

        var subAgentIds = await _agentSubAgentDataAccess
            .GetSubAgentIdsByParentAgentIdAsync(parentAgentId, cancellationToken);

        if (subAgentIds.Count == 0)
            return [];

        var aiFunctions = new List<AIFunction>();

        foreach (var subAgentId in subAgentIds)
        {
            var subAgentFunction = await BuildSubAgentAIFunctionAsync(subAgentId, depth - 1, cancellationToken);
            if (subAgentFunction is not null)
                aiFunctions.Add(subAgentFunction);
        }

        return aiFunctions;
    }

    private bool IsCliAgent(Agent agent)
    {
        if (string.IsNullOrEmpty(agent.TemplateIdentifier))
            return false;

        try
        {
            var template = _templateRegistry.GetByIdentifier(agent.TemplateIdentifier);
            return template?.IsCliAgent == true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to look up template {TemplateIdentifier} for agent {AgentId}; treating as non-CLI",
                agent.TemplateIdentifier, agent.Id);
            return false;
        }
    }

    private async Task<AIFunction?> BuildCliSubAgentAIFunctionAsync(
        Agent subAgent,
        int depth,
        CancellationToken cancellationToken)
    {
        if (subAgent.AiCliIntegrationId is null)
        {
            _logger.LogWarning(
                "CLI sub-agent {SubAgentId} has no AiCliIntegrationId configured; skipping",
                subAgent.Id);
            return null;
        }

        try
        {
            var template = _templateRegistry.GetByIdentifier(subAgent.TemplateIdentifier!);
            var isReadOnly = template?.IsReadOnlyCli ?? true;

            var instructions = subAgent.CustomInstructions ?? subAgent.ProjectPrinciples;
            AIFunction aiFunction;

            if (isReadOnly)
            {
                var cliClient = await _cliClientFactory.CreateReadOnlyClientAsync(
                    subAgent.AiCliIntegrationId.Value,
                    subAgent.Model,
                    subAgent.ReasoningEffort,
                    cancellationToken);

                aiFunction = cliClient.AsReadOnlyAgent(instructions, subAgent.Name).AsAIFunction();
            }
            else
            {
                var cliClient = await _cliClientFactory.CreateClientAsync(
                    subAgent.AiCliIntegrationId.Value,
                    subAgent.Model,
                    subAgent.ReasoningEffort,
                    cancellationToken);

                aiFunction = cliClient.AsAgent(instructions, subAgent.Name).AsAIFunction();
            }

            _logger.LogDebug(
                "Created CLI sub-agent AIFunction for agent {SubAgentName} ({SubAgentId})",
                subAgent.Name, subAgent.Id);

            return aiFunction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to build CLI sub-agent AIFunction for agent {SubAgentId}; skipping",
                subAgent.Id);
            return null;
        }
    }

    private async Task<AIFunction?> BuildSubAgentAIFunctionAsync(
        Guid subAgentId,
        int depth,
        CancellationToken cancellationToken)
    {
        var subAgent = await _agentDataAccess.GetByIdAsync(subAgentId, cancellationToken);
        if (subAgent is null)
        {
            _logger.LogWarning("Sub-agent {SubAgentId} not found; skipping", subAgentId);
            return null;
        }

        // Check if this is a CLI subagent first
        if (IsCliAgent(subAgent))
        {
            _logger.LogDebug("Resolving CLI sub-agent {SubAgentName} ({SubAgentId})", subAgent.Name, subAgent.Id);
            return await BuildCliSubAgentAIFunctionAsync(subAgent, depth, cancellationToken);
        }

        // Otherwise, it's a model-based subagent
        if (string.IsNullOrEmpty(subAgent.Model))
        {
            _logger.LogWarning(
                "Model-based sub-agent {SubAgentId} has no model configured; skipping",
                subAgentId);
            return null;
        }

        try
        {
            var chatClient = await _chatClientResolver.ResolveAsync(
                subAgent.WorkspaceId,
                subAgent.Model,
                cancellationToken);

            var subAgentTools = await GetAgentToolsInternalAsync(
                subAgentId,
                subAgent.Model,
                subAgent.ProjectPrinciples,
                depth,
                cancellationToken);

            var agentInstance = new ChatClientAgent(
                chatClient,
                instructions: subAgent.CustomInstructions ?? subAgent.ProjectPrinciples,
                name: subAgent.Name,
                tools: subAgentTools.ToArray());

            var aiFunction = agentInstance.AsAIFunction();

            _logger.LogDebug(
                "Created model-based sub-agent AIFunction for agent {SubAgentName} ({SubAgentId})",
                subAgent.Name, subAgentId);

            return aiFunction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to build sub-agent AIFunction for agent {SubAgentId}; skipping",
                subAgentId);
            return null;
        }
    }

    private async Task<List<AIFunction>> ResolveMcpToolsAsync(
        List<ToolAction> mcpActions,
        Guid agentWorkspaceId,
        CancellationToken cancellationToken)
    {
        var groups = mcpActions.GroupBy(a => a.ToolCategoryId);

        var tasks = groups.Select(g =>
            ResolveMcpToolsForCategoryAsync(g.Key, g.ToList(), agentWorkspaceId, cancellationToken));

        var results = await Task.WhenAll(tasks);

        return results.SelectMany(r => r).ToList();
    }

    private async Task<List<AIFunction>> ResolveMcpToolsForCategoryAsync(
        Guid categoryId,
        List<ToolAction> actions,
        Guid agentWorkspaceId,
        CancellationToken cancellationToken)
    {
        var mcpServer = await LoadAndValidateMcpServerAsync(categoryId, agentWorkspaceId, cancellationToken);
        if (mcpServer is null)
            return [];

        var mcpClient = await ConnectToMcpServerAsync(mcpServer, cancellationToken);
        if (mcpClient is null)
            return [];

        return await MatchAndConvertToolsAsync(mcpClient, actions, cancellationToken);
    }

    private async Task<McpServer?> LoadAndValidateMcpServerAsync(
        Guid categoryId,
        Guid agentWorkspaceId,
        CancellationToken cancellationToken)
    {
        var category = await _toolCategoryDataAccess.GetByIdAsync(categoryId, cancellationToken);

        if (category is null || category.McpServerId is null)
        {
            _logger.LogWarning("Category {CategoryId} has no MCP server configured; skipping", categoryId);
            return null;
        }

        var mcpServer = await _mcpServerDataAccess.GetByIdAsync(category.McpServerId.Value, cancellationToken);

        if (mcpServer is null)
        {
            _logger.LogWarning("MCP server {McpServerId} not found; skipping", category.McpServerId.Value);
            return null;
        }

        if (mcpServer.WorkspaceId != agentWorkspaceId)
        {
            _logger.LogWarning(
                "Cross-workspace access denied: MCP server {McpServerId} belongs to workspace {McpServerWorkspace}, agent is in {AgentWorkspace}",
                mcpServer.Id, mcpServer.WorkspaceId, agentWorkspaceId);
            return null;
        }

        return mcpServer;
    }

    private async Task<IMcpClient?> ConnectToMcpServerAsync(
        McpServer mcpServer,
        CancellationToken cancellationToken)
    {
        try
        {
            return mcpServer.TransportType == McpTransportType.STDIO
                ? await ConnectToStdioMcpServerAsync(mcpServer, cancellationToken)
                : await ConnectToHttpMcpServerAsync(mcpServer, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP connection failed for MCP server {McpServerId}; skipping tools", mcpServer.Id);
            return null;
        }
    }

    private async Task<IMcpClient> ConnectToHttpMcpServerAsync(
        McpServer mcpServer,
        CancellationToken cancellationToken)
    {
        var decryptedKey = mcpServer.EncryptedApiKey is not null
            ? _encryptionService.Decrypt(mcpServer.EncryptedApiKey)
            : null;

        return await _mcpClientFactory.GetOrCreateClientAsync(
            mcpServer.Id,
            mcpServer.EndpointUrl!,
            decryptedKey,
            cancellationToken);
    }

    private async Task<IMcpClient> ConnectToStdioMcpServerAsync(
        McpServer mcpServer,
        CancellationToken cancellationToken)
    {
        var envVars = DecryptEnvironmentVariables(mcpServer);
        var arguments = DeserializeArguments(mcpServer.Arguments);

        return await _mcpClientFactory.CreateStdioClientAsync(
            mcpServer.Command!,
            arguments,
            envVars,
            cancellationToken);
    }

    private Dictionary<string, string>? DecryptEnvironmentVariables(McpServer mcpServer)
    {
        if (string.IsNullOrEmpty(mcpServer.EncryptedEnvironmentVariables))
            return null;

        var json = _encryptionService.Decrypt(mcpServer.EncryptedEnvironmentVariables);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
    }

    private static string[]? DeserializeArguments(string? mcpArgumentsJson)
    {
        if (string.IsNullOrEmpty(mcpArgumentsJson))
            return null;

        return mcpArgumentsJson.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task<List<AIFunction>> MatchAndConvertToolsAsync(
        IMcpClient mcpClient,
        List<ToolAction> actions,
        CancellationToken cancellationToken)
    {
        IEnumerable<IMcpToolDescriptor> serverTools;

        try
        {
            serverTools = await mcpClient.ListToolsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list tools from MCP server");
            return [];
        }

        var serverToolLookup = serverTools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var aiFunctions = new List<AIFunction>();

        foreach (var action in actions)
        {
            if (!serverToolLookup.TryGetValue(action.MethodName, out var serverTool))
            {
                _logger.LogWarning("MCP tool '{MethodName}' no longer exists on server; skipping", action.MethodName);
                continue;
            }

            aiFunctions.Add(serverTool.AsAIFunction());
        }

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