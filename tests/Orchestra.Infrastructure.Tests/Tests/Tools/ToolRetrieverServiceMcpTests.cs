using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Exceptions;
using Orchestra.Infrastructure.AiCliIntegrations;
using Orchestra.Infrastructure.Tools;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Infrastructure.Tests.Tests.Tools;

public class ToolRetrieverServiceMcpTests
{
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess = Substitute.For<IToolCategoryDataAccess>();
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
    private readonly IMcpClientFactory _mcpClientFactory = Substitute.For<IMcpClientFactory>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly IProviderCredentialEncryptionService _encryptionService = Substitute.For<IProviderCredentialEncryptionService>();
    private readonly IMcpServerDataAccess _mcpServerDataAccess = Substitute.For<IMcpServerDataAccess>();
    private readonly IAgentMcpToolDataAccess _agentMcpToolDataAccess = Substitute.For<IAgentMcpToolDataAccess>();
    private readonly IAgentSubAgentDataAccess _agentSubAgentDataAccess = Substitute.For<IAgentSubAgentDataAccess>();
    private readonly IMcpClient _mcpClient = Substitute.For<IMcpClient>();
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry = Substitute.For<IBuiltInAgentTemplateRegistry>();
    private readonly IAiCliClientFactory _cliClientFactory = Substitute.For<IAiCliClientFactory>();
    private readonly ILogger<ToolRetrieverService> _logger = Substitute.For<ILogger<ToolRetrieverService>>();
    private readonly ToolRetrieverService _sut;

    public ToolRetrieverServiceMcpTests()
    {
        _agentSubAgentDataAccess
            .GetSubAgentIdsByParentAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());

        _agentMcpToolDataAccess
            .GetByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentMcpTool>());

        _sut = new ToolRetrieverService(
            _serviceProvider,
            _toolActionDataAccess,
            _toolCategoryDataAccess,
            _agentToolActionDataAccess,
            _mcpClientFactory,
            _integrationDataAccess,
            _agentDataAccess,
            _encryptionService,
            _mcpServerDataAccess,
            _agentMcpToolDataAccess,
            _agentSubAgentDataAccess,
            Substitute.For<IChatClientResolver>(),
            Substitute.For<IChatAgentRunner>(),
            _templateRegistry,
            _cliClientFactory,
            Substitute.For<IAgentQuestionRepository>(),
            Substitute.For<INotificationService>(),
            _logger);
    }

    // -------------------------------------------------------------------------
    // Scenario 1: Successfully resolve MCP tools into AIFunctions (AC-1)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithMcpToolAction_ReturnsAIFunctionFromServer()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder()
            .WithMethodName("get_design_tokens")
            .AsMcpTool(Guid.NewGuid())
            .Build();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var serverTool = new FakeMcpToolDescriptor("get_design_tokens");

        ArrangeLegacyMcpResolution(agentId, mcpToolAction, agent, mcpServerId, workspaceId, serverTool);

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Single(result);
    }

    // -------------------------------------------------------------------------
    // Scenario 2: Group MCP tools by category — single client reused (AC-2)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithFiveMcpToolsFromSameMcpServer_CreatesOnlyOneMcpClient()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var toolActionIds = new List<Guid>();
        var toolActions = new List<ToolAction>();
        var serverTools = new List<IMcpToolDescriptor>();

        for (var i = 0; i < 5; i++)
        {
            var action = new ToolActionBuilder()
                .WithToolCategoryId(categoryId)
                .WithMethodName($"tool_{i}")
                .AsMcpTool(Guid.NewGuid())
                .Build();
            toolActionIds.Add(action.Id);
            toolActions.Add(action);
            serverTools.Add(new FakeMcpToolDescriptor($"tool_{i}"));
        }

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var mcpServer = new McpServerBuilder().WithId(mcpServerId).WithWorkspaceId(workspaceId).WithEndpointUrl("https://mcp.example.com").Build();
        var category = ToolCategory.CreateForMcpServer("Test", "desc", ProviderType.MCP_GENERIC, mcpServerId);

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(toolActionIds);

        foreach (var action in toolActions)
            _toolActionDataAccess.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(categoryId, Arg.Any<CancellationToken>()).Returns(category);
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns(mcpServer);
        _mcpClientFactory.GetOrCreateClientAsync(mcpServerId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_mcpClient);
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(serverTools.AsEnumerable());

        await _sut.GetAgentToolsAsync(agentId);

        await _mcpClientFactory.Received(1)
            .GetOrCreateClientAsync(mcpServerId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 3: MCP server unreachable — agent continues with native tools (AC-3)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WhenMcpServerUnreachable_SkipsMcpToolsAndReturnsEmpty()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder()
            .WithMethodName("get_design_tokens")
            .AsMcpTool(Guid.NewGuid())
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var mcpServer = new McpServerBuilder().WithId(mcpServerId).WithWorkspaceId(workspaceId).WithEndpointUrl("https://mcp.example.com").Build();
        var category = ToolCategory.CreateForMcpServer("Test", "desc", ProviderType.MCP_GENERIC, mcpServerId);

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>()).Returns(mcpToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(mcpToolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(category);
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns(mcpServer);

        _mcpClientFactory.GetOrCreateClientAsync(mcpServerId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAgentToolsAsync_WhenMcpServerUnreachable_ExecutionContinuesWithoutException()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder().WithMethodName("tool_a").AsMcpTool(Guid.NewGuid()).Build();
        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var mcpServer = new McpServerBuilder().WithId(mcpServerId).WithWorkspaceId(workspaceId).WithEndpointUrl("https://unreachable.example.com").Build();
        var category = ToolCategory.CreateForMcpServer("Test", "desc", ProviderType.MCP_GENERIC, mcpServerId);

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<Guid> { mcpToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>()).Returns(mcpToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(mcpToolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(category);
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns(mcpServer);
        _mcpClientFactory.GetOrCreateClientAsync(mcpServerId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("Timeout"));

        var exception = await Record.ExceptionAsync(() => _sut.GetAgentToolsAsync(agentId));

        Assert.Null(exception);
    }

    // -------------------------------------------------------------------------
    // Scenario 4: MCP tool removed from server — skipped with warning (AC-4)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WhenMcpToolNoLongerExistsOnServer_SkipsThatToolAndContinues()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var removedAction = new ToolActionBuilder()
            .WithToolCategoryId(categoryId)
            .WithMethodName("old_export_svg")
            .AsMcpTool(Guid.NewGuid())
            .Build();

        var existingAction = new ToolActionBuilder()
            .WithToolCategoryId(categoryId)
            .WithMethodName("get_design_tokens")
            .AsMcpTool(Guid.NewGuid())
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var mcpServer = new McpServerBuilder().WithId(mcpServerId).WithWorkspaceId(workspaceId).WithEndpointUrl("https://mcp.example.com").Build();
        var category = ToolCategory.CreateForMcpServer("Test", "desc", ProviderType.MCP_GENERIC, mcpServerId);
        var serverTools = new List<IMcpToolDescriptor> { new FakeMcpToolDescriptor("get_design_tokens") };

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { removedAction.Id, existingAction.Id });
        _toolActionDataAccess.GetByIdAsync(removedAction.Id, Arg.Any<CancellationToken>()).Returns(removedAction);
        _toolActionDataAccess.GetByIdAsync(existingAction.Id, Arg.Any<CancellationToken>()).Returns(existingAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(categoryId, Arg.Any<CancellationToken>()).Returns(category);
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns(mcpServer);
        _mcpClientFactory.GetOrCreateClientAsync(mcpServerId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_mcpClient);
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(serverTools.AsEnumerable());

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Single(result);
    }

    // -------------------------------------------------------------------------
    // Scenario 5: Cross-workspace server access rejected (AC-5)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithCrossWorkspaceMcpServer_SkipsToolsWithWarning()
    {
        var agentWorkspaceId = Guid.NewGuid();
        var foreignWorkspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder()
            .WithMethodName("get_design_tokens")
            .AsMcpTool(Guid.NewGuid())
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(agentWorkspaceId).Build();
        var foreignServer = new McpServerBuilder().WithId(mcpServerId).WithWorkspaceId(foreignWorkspaceId).WithEndpointUrl("https://mcp.example.com").Build();
        var category = ToolCategory.CreateForMcpServer("Test", "desc", ProviderType.MCP_GENERIC, mcpServerId);

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>()).Returns(mcpToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(mcpToolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(category);
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns(foreignServer);

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
        await _mcpClientFactory.DidNotReceive()
            .GetOrCreateClientAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Edge case: Category has no McpServerId — tools skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WhenCategoryHasNoMcpServerId_SkipsAllToolsFromThatCategory()
    {
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder().WithMethodName("tool_x").AsMcpTool(Guid.NewGuid()).Build();
        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        // Category created WITHOUT McpServerId (McpServerId = null)
        var categoryWithoutServer = ToolCategory.Create("No-server category", "desc", ProviderType.MCP_GENERIC, "SomeService");

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>()).Returns(mcpToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(mcpToolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(categoryWithoutServer);

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // Edge case: McpServer not found in DB — tools skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WhenMcpServerNotFoundInDb_SkipsAllToolsFromThatCategory()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder().WithMethodName("tool_y").AsMcpTool(Guid.NewGuid()).Build();
        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var category = ToolCategory.CreateForMcpServer("Test", "desc", ProviderType.MCP_GENERIC, mcpServerId);

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>()).Returns(mcpToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(mcpToolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(category);
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns((McpServer?)null);

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // Edge case: No MCP tools assigned — MCP branch not entered, no extra calls
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithNoMcpTools_DoesNotCallMcpClientFactory()
    {
        var agentId = Guid.NewGuid();
        var agent = new AgentBuilder().WithId(agentId).Build();

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);

        await _sut.GetAgentToolsAsync(agentId);

        await _mcpClientFactory.DidNotReceive()
            .GetOrCreateClientAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // FR-006 Scenario 2: Stdio MCP tool — resolved successfully (AC-2)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithStdioMcpTool_CallsCreateStdioClientAndReturnsAIFunction()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var toolAction = new ToolActionBuilder()
            .WithMethodName("read_file")
            .AsMcpTool(Guid.NewGuid())
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var stdioServer = new McpServerBuilder()
            .WithId(mcpServerId)
            .WithWorkspaceId(workspaceId)
            .WithCommand("npx")
            .WithArguments("""["@modelcontextprotocol/server-filesystem"]""")
            .WithEncryptedEnvironmentVariables("encrypted_env_blob")
            .Build();
        var category = ToolCategory.CreateForMcpServer("Test", "desc", ProviderType.MCP_GENERIC, mcpServerId);

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { toolAction.Id });
        _toolActionDataAccess.GetByIdAsync(toolAction.Id, Arg.Any<CancellationToken>()).Returns(toolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(toolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(category);
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns(stdioServer);
        _encryptionService.Decrypt("encrypted_env_blob").Returns("""{"PATH":"/usr/bin"}""");
        _mcpClientFactory
            .CreateStdioClientAsync("npx", Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(_mcpClient);
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<IMcpToolDescriptor> { new FakeMcpToolDescriptor("read_file") }.AsEnumerable());

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Single(result);
        await _mcpClientFactory.Received(1)
            .CreateStdioClientAsync(Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
        await _mcpClientFactory.DidNotReceive()
            .GetOrCreateClientAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // FR-006 Scenario 4: Stdio process fails to start — agent continues (AC-4)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WhenStdioProcessFailsToStart_AgentExecutionContinuesWithoutException()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var toolAction = new ToolActionBuilder()
            .WithMethodName("run_command")
            .AsMcpTool(Guid.NewGuid())
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var stdioServer = new McpServerBuilder()
            .WithId(mcpServerId)
            .WithWorkspaceId(workspaceId)
            .WithCommand("nonexistent-command")
            .WithArguments(null)
            .Build();
        var category = ToolCategory.CreateForMcpServer("Test", "desc", ProviderType.MCP_GENERIC, mcpServerId);

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { toolAction.Id });
        _toolActionDataAccess.GetByIdAsync(toolAction.Id, Arg.Any<CancellationToken>()).Returns(toolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(toolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(category);
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns(stdioServer);
        _encryptionService.Decrypt(Arg.Any<string>()).Returns("{}");
        _mcpClientFactory
            .CreateStdioClientAsync(Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(_mcpClient);
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProcessLaunchException("nonexistent-command"));

        var exception = await Record.ExceptionAsync(() => _sut.GetAgentToolsAsync(agentId));

        Assert.Null(exception);
        await _mcpClientFactory.Received(1)
            .CreateStdioClientAsync(Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // FR-006 Scenario 5: Tool with null IntegrationId (parent deleted) — skipped (AC-5)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithOrphanedMcpToolNullIntegrationId_SkipsToolWithWarning()
    {
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var orphanedTool = new ToolActionBuilder()
            .AsOrphanedMcpTool()
            .WithMethodName("stale_tool")
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        // Category with no McpServerId — simulates the orphaned/deleted state
        var orphanedCategory = ToolCategory.Create("Orphaned", "desc", ProviderType.MCP_GENERIC, "SomeService");

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { orphanedTool.Id });
        _toolActionDataAccess.GetByIdAsync(orphanedTool.Id, Arg.Any<CancellationToken>()).Returns(orphanedTool);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(orphanedTool.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(orphanedCategory);

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
        await _mcpClientFactory.DidNotReceive()
            .GetOrCreateClientAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _mcpClientFactory.DidNotReceive()
            .CreateStdioClientAsync(Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Connected MCP Tools (AgentMcpTools table) — new path
    // =========================================================================

    // -------------------------------------------------------------------------
    // Connected Scenario 1: Happy path — connected tool returned as AIFunction
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithConnectedMcpTool_ReturnsAIFunction()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var mcpServer = new McpServerBuilder().WithId(mcpServerId).WithWorkspaceId(workspaceId).WithEndpointUrl("https://mcp.example.com").Build();
        var connectedTool = AgentMcpTool.Create(agentId, mcpServerId, "list_issues");

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<Guid>());
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _agentMcpToolDataAccess.GetByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<AgentMcpTool> { connectedTool });
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns(mcpServer);
        _mcpClientFactory.GetOrCreateClientAsync(mcpServerId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(_mcpClient);
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<IMcpToolDescriptor> { new FakeMcpToolDescriptor("list_issues") }.AsEnumerable());

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Single(result);
    }

    // -------------------------------------------------------------------------
    // Connected Scenario 2: Tool name not on server — skipped with warning
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithConnectedMcpTool_WhenToolNotOnServer_SkipsWithWarning()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var mcpServer = new McpServerBuilder().WithId(mcpServerId).WithWorkspaceId(workspaceId).WithEndpointUrl("https://mcp.example.com").Build();
        var connectedTool = AgentMcpTool.Create(agentId, mcpServerId, "deleted_tool");

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<Guid>());
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _agentMcpToolDataAccess.GetByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<AgentMcpTool> { connectedTool });
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns(mcpServer);
        _mcpClientFactory.GetOrCreateClientAsync(mcpServerId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(_mcpClient);
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<IMcpToolDescriptor> { new FakeMcpToolDescriptor("other_tool") }.AsEnumerable());

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // Connected Scenario 3: Server workspace mismatch — skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithConnectedMcpTool_WhenServerBelongsToDifferentWorkspace_SkipsTool()
    {
        var agentWorkspaceId = Guid.NewGuid();
        var foreignWorkspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(agentWorkspaceId).Build();
        var foreignServer = new McpServerBuilder().WithId(mcpServerId).WithWorkspaceId(foreignWorkspaceId).WithEndpointUrl("https://mcp.example.com").Build();
        var connectedTool = AgentMcpTool.Create(agentId, mcpServerId, "some_tool");

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<Guid>());
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _agentMcpToolDataAccess.GetByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<AgentMcpTool> { connectedTool });
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns(foreignServer);

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
        await _mcpClientFactory.DidNotReceive()
            .GetOrCreateClientAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Connected Scenario 4: Server not found in DB — skipped, no exception
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithConnectedMcpTool_WhenServerNotFoundInDb_SkipsWithoutException()
    {
        var workspaceId = Guid.NewGuid();
        var mcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var connectedTool = AgentMcpTool.Create(agentId, mcpServerId, "some_tool");

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<Guid>());
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _agentMcpToolDataAccess.GetByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<AgentMcpTool> { connectedTool });
        _mcpServerDataAccess.GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>()).Returns((McpServer?)null);

        var exception = await Record.ExceptionAsync(() => _sut.GetAgentToolsAsync(agentId));
        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Null(exception);
        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // Connected Scenario 5: Both legacy ToolAction MCP + connected MCP → both returned
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithBothLegacyAndConnectedMcpTools_ReturnsBothAIFunctions()
    {
        var workspaceId = Guid.NewGuid();
        var legacyMcpServerId = Guid.NewGuid();
        var connectedMcpServerId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var legacyMcpClient = Substitute.For<IMcpClient>();
        var connectedMcpClient = Substitute.For<IMcpClient>();

        var legacyToolAction = new ToolActionBuilder().WithMethodName("legacy_tool").AsMcpTool(Guid.NewGuid()).Build();
        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var legacyServer = new McpServerBuilder().WithId(legacyMcpServerId).WithWorkspaceId(workspaceId).WithEndpointUrl("https://legacy.mcp.example.com").Build();
        var legacyCategory = ToolCategory.CreateForMcpServer("Legacy", "desc", ProviderType.MCP_GENERIC, legacyMcpServerId);
        var connectedServer = new McpServerBuilder().WithId(connectedMcpServerId).WithWorkspaceId(workspaceId).WithEndpointUrl("https://connected.mcp.example.com").Build();
        var connectedTool = AgentMcpTool.Create(agentId, connectedMcpServerId, "connected_tool");

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<Guid> { legacyToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(legacyToolAction.Id, Arg.Any<CancellationToken>()).Returns(legacyToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _toolCategoryDataAccess.GetByIdAsync(legacyToolAction.ToolCategoryId, Arg.Any<CancellationToken>()).Returns(legacyCategory);
        _mcpServerDataAccess.GetByIdAsync(legacyMcpServerId, Arg.Any<CancellationToken>()).Returns(legacyServer);
        _mcpServerDataAccess.GetByIdAsync(connectedMcpServerId, Arg.Any<CancellationToken>()).Returns(connectedServer);
        _agentMcpToolDataAccess.GetByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<AgentMcpTool> { connectedTool });
        _mcpClientFactory.GetOrCreateClientAsync(legacyMcpServerId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(legacyMcpClient);
        _mcpClientFactory.GetOrCreateClientAsync(connectedMcpServerId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(connectedMcpClient);
        legacyMcpClient.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<IMcpToolDescriptor> { new FakeMcpToolDescriptor("legacy_tool") }.AsEnumerable());
        connectedMcpClient.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<IMcpToolDescriptor> { new FakeMcpToolDescriptor("connected_tool") }.AsEnumerable());

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Equal(2, result.Count());
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void ArrangeLegacyMcpResolution(
        Guid agentId,
        ToolAction mcpToolAction,
        Agent agent,
        Guid mcpServerId,
        Guid workspaceId,
        FakeMcpToolDescriptor serverTool)
    {
        var mcpServer = new McpServerBuilder().WithId(mcpServerId).WithWorkspaceId(workspaceId).WithEndpointUrl("https://mcp.example.com").Build();
        var category = ToolCategory.CreateForMcpServer("Test", "desc", ProviderType.MCP_GENERIC, mcpServerId);

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpToolAction.Id });

        _toolActionDataAccess
            .GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>())
            .Returns(mcpToolAction);

        _agentDataAccess
            .GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);

        _toolCategoryDataAccess
            .GetByIdAsync(mcpToolAction.ToolCategoryId, Arg.Any<CancellationToken>())
            .Returns(category);

        _mcpServerDataAccess
            .GetByIdAsync(mcpServerId, Arg.Any<CancellationToken>())
            .Returns(mcpServer);

        _mcpClientFactory
            .GetOrCreateClientAsync(mcpServerId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_mcpClient);

        _mcpClient
            .ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<IMcpToolDescriptor> { serverTool }.AsEnumerable());
    }
}

// -------------------------------------------------------------------------
// Test double — implements IMcpToolDescriptor with AsAIFunction support
// -------------------------------------------------------------------------
internal sealed class FakeMcpToolDescriptor(string name, string? description = null)
    : Orchestra.Application.Common.Interfaces.IMcpToolDescriptor
{
    public string Name { get; } = name;
    public string? Description { get; } = description;

    public AIFunction AsAIFunction()
    {
        Func<Task> stub = () => Task.CompletedTask;
        return AIFunctionFactory.Create(stub, Name, Description ?? string.Empty);
    }
}
