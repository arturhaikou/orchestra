using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Exceptions;
using Orchestra.Infrastructure.Tools;

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
    private readonly IMcpClient _mcpClient = Substitute.For<IMcpClient>();
    private readonly ILogger<ToolRetrieverService> _logger = Substitute.For<ILogger<ToolRetrieverService>>();
    private readonly ToolRetrieverService _sut;

    public ToolRetrieverServiceMcpTests()
    {
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
            _logger);
    }

    // -------------------------------------------------------------------------
    // Scenario 1: Successfully resolve MCP tools into AIFunctions (AC-1)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithMcpToolAction_ReturnsAIFunctionFromServer()
    {
        var workspaceId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder()
            .WithMethodName("get_design_tokens")
            .AsMcpTool(integrationId)
            .Build();

        var agent = new AgentBuilder()
            .WithId(agentId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(workspaceId)
            .WithMcpBacked(true)
            .WithIsActive(true)
            .WithMcpEndpointUrl("https://figma.mcp.example.com")
            .Build();

        var serverTool = new FakeMcpToolDescriptor("get_design_tokens");

        ArrangeMcpResolution(agentId, mcpToolAction, agent, integration, serverTool);

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Single(result);
    }

    // -------------------------------------------------------------------------
    // Scenario 2: Group MCP tools by integration — single client reused (AC-2)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithFiveMcpToolsFromSameIntegration_CreatesOnlyOneMcpClient()
    {
        var workspaceId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var toolActionIds = new List<Guid>();
        var toolActions = new List<ToolAction>();
        var serverTools = new List<IMcpToolDescriptor>();

        for (var i = 0; i < 5; i++)
        {
            var action = new ToolActionBuilder()
                .WithMethodName($"tool_{i}")
                .AsMcpTool(integrationId)
                .Build();
            toolActionIds.Add(action.Id);
            toolActions.Add(action);
            serverTools.Add(new FakeMcpToolDescriptor($"tool_{i}"));
        }

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var integration = new IntegrationBuilder()
            .WithId(integrationId).WithWorkspaceId(workspaceId)
            .WithMcpBacked(true).WithIsActive(true)
            .WithMcpEndpointUrl("https://figma.mcp.example.com").Build();

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(toolActionIds);

        foreach (var action in toolActions)
            _toolActionDataAccess.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);

        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _encryptionService.Decrypt(Arg.Any<string>()).Returns("decrypted-key");
        _mcpClientFactory.GetOrCreateClientAsync(integrationId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_mcpClient);
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(serverTools.AsEnumerable());

        await _sut.GetAgentToolsAsync(agentId);

        await _mcpClientFactory.Received(1)
            .GetOrCreateClientAsync(integrationId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 3: MCP server unreachable — agent continues with native tools (AC-3)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WhenMcpServerUnreachable_SkipsMcpToolsAndReturnsEmptyForThatIntegration()
    {
        var workspaceId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder()
            .WithMethodName("get_design_tokens")
            .AsMcpTool(integrationId)
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var integration = new IntegrationBuilder()
            .WithId(integrationId).WithWorkspaceId(workspaceId)
            .WithMcpBacked(true).WithIsActive(true)
            .WithMcpEndpointUrl("https://figma.mcp.example.com").Build();

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>()).Returns(mcpToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _encryptionService.Decrypt(Arg.Any<string>()).Returns("decrypted-key");

        _mcpClientFactory.GetOrCreateClientAsync(integrationId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAgentToolsAsync_WhenMcpServerUnreachable_ExecutionContinuesWithoutException()
    {
        var workspaceId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder().WithMethodName("tool_a").AsMcpTool(integrationId).Build();
        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var integration = new IntegrationBuilder()
            .WithId(integrationId).WithWorkspaceId(workspaceId)
            .WithMcpBacked(true).WithIsActive(true)
            .WithMcpEndpointUrl("https://unreachable.example.com").Build();

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(new List<Guid> { mcpToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>()).Returns(mcpToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _encryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpClientFactory.GetOrCreateClientAsync(integrationId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
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
        var integrationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var removedAction = new ToolActionBuilder()
            .WithMethodName("old_export_svg")
            .AsMcpTool(integrationId)
            .Build();

        var existingAction = new ToolActionBuilder()
            .WithMethodName("get_design_tokens")
            .AsMcpTool(integrationId)
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var integration = new IntegrationBuilder()
            .WithId(integrationId).WithWorkspaceId(workspaceId)
            .WithMcpBacked(true).WithIsActive(true)
            .WithMcpEndpointUrl("https://figma.mcp.example.com").Build();

        var serverTools = new List<IMcpToolDescriptor> { new FakeMcpToolDescriptor("get_design_tokens") };

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { removedAction.Id, existingAction.Id });
        _toolActionDataAccess.GetByIdAsync(removedAction.Id, Arg.Any<CancellationToken>()).Returns(removedAction);
        _toolActionDataAccess.GetByIdAsync(existingAction.Id, Arg.Any<CancellationToken>()).Returns(existingAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _encryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpClientFactory.GetOrCreateClientAsync(integrationId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_mcpClient);
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(serverTools.AsEnumerable());

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Single(result);
    }

    // -------------------------------------------------------------------------
    // Scenario 5: Cross-workspace integration access rejected (AC-5)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WithCrossWorkspaceIntegration_SkipsToolsWithWarning()
    {
        var agentWorkspaceId = Guid.NewGuid();
        var foreignWorkspaceId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder()
            .WithMethodName("get_design_tokens")
            .AsMcpTool(integrationId)
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(agentWorkspaceId).Build();

        var foreignIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(foreignWorkspaceId)
            .WithMcpBacked(true).WithIsActive(true)
            .WithMcpEndpointUrl("https://figma.mcp.example.com").Build();

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>()).Returns(mcpToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(foreignIntegration);

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
        await _mcpClientFactory.DidNotReceive()
            .GetOrCreateClientAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Edge case: Integration inactive — tools skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WhenIntegrationInactive_SkipsAllToolsFromThatIntegration()
    {
        var workspaceId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder().WithMethodName("tool_x").AsMcpTool(integrationId).Build();
        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var inactiveIntegration = new IntegrationBuilder()
            .WithId(integrationId).WithWorkspaceId(workspaceId)
            .WithMcpBacked(true).WithIsActive(false)
            .WithMcpEndpointUrl("https://figma.mcp.example.com").Build();

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>()).Returns(mcpToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(inactiveIntegration);

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // Edge case: Integration not MCP-backed — tools skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAgentToolsAsync_WhenIntegrationNotMcpBacked_SkipsAllToolsFromThatIntegration()
    {
        var workspaceId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var mcpToolAction = new ToolActionBuilder().WithMethodName("tool_y").AsMcpTool(integrationId).Build();
        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();
        var nonMcpIntegration = new IntegrationBuilder()
            .WithId(integrationId).WithWorkspaceId(workspaceId)
            .WithMcpBacked(false).WithIsActive(true)
            .Build();

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpToolAction.Id });
        _toolActionDataAccess.GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>()).Returns(mcpToolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(nonMcpIntegration);

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

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());

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
        var integrationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var toolAction = new ToolActionBuilder()
            .WithMethodName("read_file")
            .AsMcpTool(integrationId)
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();

        var stdioIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(workspaceId)
            .WithMcpBacked(true)
            .WithIsActive(true)
            .WithMcpTransportType(McpTransportType.STDIO)
            .WithMcpCommand("npx")
            .WithMcpArgumentsJson("""["@modelcontextprotocol/server-filesystem"]""")
            .WithMcpEncryptedEnvironmentVariables("encrypted_env_blob")
            .Build();

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { toolAction.Id });
        _toolActionDataAccess.GetByIdAsync(toolAction.Id, Arg.Any<CancellationToken>()).Returns(toolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(stdioIntegration);
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
        var integrationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var toolAction = new ToolActionBuilder()
            .WithMethodName("run_command")
            .AsMcpTool(integrationId)
            .Build();

        var agent = new AgentBuilder().WithId(agentId).WithWorkspaceId(workspaceId).Build();

        var stdioIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(workspaceId)
            .WithMcpBacked(true)
            .WithIsActive(true)
            .WithMcpTransportType(McpTransportType.STDIO)
            .WithMcpCommand("nonexistent-command")
            .Build();

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { toolAction.Id });
        _toolActionDataAccess.GetByIdAsync(toolAction.Id, Arg.Any<CancellationToken>()).Returns(toolAction);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(stdioIntegration);
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

        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { orphanedTool.Id });
        _toolActionDataAccess.GetByIdAsync(orphanedTool.Id, Arg.Any<CancellationToken>()).Returns(orphanedTool);
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);

        var result = await _sut.GetAgentToolsAsync(agentId);

        Assert.Empty(result);
        await _mcpClientFactory.DidNotReceive()
            .GetOrCreateClientAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _mcpClientFactory.DidNotReceive()
            .CreateStdioClientAsync(Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ArrangeMcpResolution(
        Guid agentId,
        ToolAction mcpToolAction,
        Agent agent,
        Integration integration,
        FakeMcpToolDescriptor serverTool)
    {
        _agentToolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { mcpToolAction.Id });

        _toolActionDataAccess
            .GetByIdAsync(mcpToolAction.Id, Arg.Any<CancellationToken>())
            .Returns(mcpToolAction);

        _agentDataAccess
            .GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(agent);

        _integrationDataAccess
            .GetByIdAsync(mcpToolAction.IntegrationId!.Value, Arg.Any<CancellationToken>())
            .Returns(integration);

        _encryptionService.Decrypt(Arg.Any<string>()).Returns("decrypted-key");

        _mcpClientFactory
            .GetOrCreateClientAsync(mcpToolAction.IntegrationId.Value, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
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
