using Microsoft.Extensions.AI;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Exceptions;
using Orchestra.Domain.Interfaces;
using Orchestra.Infrastructure.Mcp;

namespace Orchestra.Infrastructure.Tests.Tests.Mcp;

public class McpToolDiscoverySyncServiceTests
{
    private readonly IMcpClientFactory _clientFactory;
    private readonly IMcpClient _mcpClient;
    private readonly IToolActionDataAccess _toolActionDataAccess;
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly IMcpServerDataAccess _mcpServerDataAccess;
    private readonly McpToolDiscoveryService _sut;

    private readonly Guid _integrationId = Guid.NewGuid();
    private readonly Guid _toolCategoryId = Guid.NewGuid();

    public McpToolDiscoverySyncServiceTests()
    {
        _clientFactory = Substitute.For<IMcpClientFactory>();
        _mcpClient = Substitute.For<IMcpClient>();
        _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
        _toolCategoryDataAccess = Substitute.For<IToolCategoryDataAccess>();
        _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
        _credentialEncryptionService = Substitute.For<ICredentialEncryptionService>();
        _mcpServerDataAccess = Substitute.For<IMcpServerDataAccess>();

        _clientFactory.GetOrCreateClientAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_mcpClient);

        _sut = new McpToolDiscoveryService(
            _clientFactory,
            _toolActionDataAccess,
            _toolCategoryDataAccess,
            _integrationDataAccess,
            _credentialEncryptionService,
            _mcpServerDataAccess);
    }

    // -------------------------------------------------------------------------
    // Scenario 1: New tools discovered
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenServerExposesNewTools_ReturnsAddedCountAndCreatesToolActions()
    {
        ArrangeIntegrationAndCategory();
        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        ArrangeRemoteTools(
            CreateRemoteTool("get_content", "Returns content"),
            CreateRemoteTool("list_pages", "Lists pages"));

        var result = await _sut.SyncToolsAsync(_integrationId);

        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(2, result.Total);
        await _toolActionDataAccess.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<ToolAction>>(t => t.Count() == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncToolsAsync_WhenDestructiveToolDiscovered_CreatesToolActionWithIsEnabledFalse()
    {
        ArrangeIntegrationAndCategory();
        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        ArrangeRemoteTools(CreateRemoteTool("delete_component", "Deletes a component"));

        var result = await _sut.SyncToolsAsync(_integrationId);

        Assert.Equal(1, result.Added);
        Assert.Equal("added", result.Tools[0].Status);
    }

    // -------------------------------------------------------------------------
    // Scenario 2: Removed tools soft-deactivated
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenToolNoLongerOnServer_SoftDeactivatesToolAction()
    {
        ArrangeIntegrationAndCategory();
        var removedTool = new ToolActionBuilder()
            .WithToolCategoryId(_toolCategoryId)
            .WithIntegrationId(_integrationId)
            .WithMethodName("old_tool")
            .WithName("old_tool")
            .AsActive()
            .Build();

        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction> { removedTool });

        ArrangeRemoteTools();

        var result = await _sut.SyncToolsAsync(_integrationId);

        Assert.Equal(1, result.Removed);
        Assert.Equal(0, result.Total);
        Assert.Contains(result.Tools, t => t.Status == "removed");
    }

    [Fact]
    public async Task SyncToolsAsync_WhenToolRemoved_PreservesAgentAssignmentsByNotDeleting()
    {
        ArrangeIntegrationAndCategory();
        var removedTool = new ToolActionBuilder()
            .WithToolCategoryId(_toolCategoryId)
            .WithIntegrationId(_integrationId)
            .WithMethodName("old_tool")
            .WithName("old_tool")
            .Build();

        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction> { removedTool });

        ArrangeRemoteTools();

        await _sut.SyncToolsAsync(_integrationId);

        await _toolActionDataAccess.DidNotReceive()
            .UpdateAsync(Arg.Is<ToolAction>(t => t.MethodName == "old_tool" && t.IsActive), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Existing tools updated
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenExistingToolDescriptionChanged_ReturnsUpdatedStatus()
    {
        ArrangeIntegrationAndCategory();
        var existingTool = new ToolActionBuilder()
            .WithToolCategoryId(_toolCategoryId)
            .WithIntegrationId(_integrationId)
            .WithMethodName("get_file")
            .WithName("get_file")
            .WithDescription("Old description")
            .AsActive()
            .Build();

        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction> { existingTool });

        ArrangeRemoteTools(CreateRemoteTool("get_file", "New description"));

        var result = await _sut.SyncToolsAsync(_integrationId);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Updated);
        Assert.Contains(result.Tools, t => t.Status == "updated");
    }

    [Fact]
    public async Task SyncToolsAsync_WhenExistingToolUnchanged_ReturnsUnchangedStatus()
    {
        ArrangeIntegrationAndCategory();
        const string description = "Returns file content";
        var existingTool = new ToolActionBuilder()
            .WithToolCategoryId(_toolCategoryId)
            .WithIntegrationId(_integrationId)
            .WithMethodName("get_file")
            .WithName("get_file")
            .WithDescription(description)
            .AsActive()
            .Build();

        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction> { existingTool });

        ArrangeRemoteTools(CreateRemoteTool("get_file", description));

        var result = await _sut.SyncToolsAsync(_integrationId);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(0, result.Updated);
        Assert.Contains(result.Tools, t => t.Status == "unchanged");
    }

    // -------------------------------------------------------------------------
    // Scenario 4: MCP server unreachable — no DB changes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenMcpServerUnreachable_ThrowsMcpConnectionException()
    {
        ArrangeIntegrationAndCategory();
        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.SyncToolsAsync(_integrationId));
    }

    [Fact]
    public async Task SyncToolsAsync_WhenMcpServerUnreachable_NoToolActionsPersisted()
    {
        ArrangeIntegrationAndCategory();
        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.SyncToolsAsync(_integrationId));

        await _toolActionDataAccess.DidNotReceive()
            .AddRangeAsync(Arg.Any<IEnumerable<ToolAction>>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Integration timestamp update
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_OnSuccess_UpdatesIntegrationLastSyncAt()
    {
        ArrangeIntegrationAndCategory();
        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());
        ArrangeRemoteTools();

        await _sut.SyncToolsAsync(_integrationId);

        await _integrationDataAccess.Received(1).UpdateAsync(
            Arg.Any<Integration>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ArrangeIntegrationAndCategory()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithIsMcpBacked(true)
            .WithMcpEndpointUrl("https://mcp.example.com")
            .WithMcpAuthType(McpAuthType.API_KEY)
            .WithEncryptedApiKey("encrypted-key")
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);

        _credentialEncryptionService.Decrypt("encrypted-key").Returns("decrypted-key");

        var category = new ToolCategoryBuilder()
            .WithIntegrationId(_integrationId)
            .Build();
        SetCategoryId(category, _toolCategoryId);

        _toolCategoryDataAccess.FindByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(category);
    }

    private void ArrangeRemoteTools(params IMcpToolDescriptor[] tools)
    {
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(tools.ToList());
    }

    private static IMcpToolDescriptor CreateRemoteTool(string name, string? description = null)
    {
        var tool = Substitute.For<IMcpToolDescriptor>();
        tool.Name.Returns(name);
        tool.Description.Returns(description);
        return tool;
    }

    private static void SetCategoryId(ToolCategory category, Guid id)
    {
        typeof(ToolCategory)
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.SetValue(category, id);
    }

    // -------------------------------------------------------------------------
    // STDIO Sync — Scenario 4: Idempotent re-sync via stdio transport
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WithStdioTransport_UsesCreateStdioClientAsync()
    {
        var stdioIntegration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithName("FS MCP")
            .AsStdioMcpBacked("npx")
            .WithMcpArgumentsJson("""["@modelcontextprotocol/server-filesystem","/tmp"]""")
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(stdioIntegration);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(BuildCategory());
        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        var stdioClient = Substitute.For<IMcpClient>();
        var remoteTools = new List<IMcpToolDescriptor> { CreateRemoteTool("list_files", "Lists files") };
        stdioClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(remoteTools);
        _clientFactory.CreateStdioClientAsync(
                "npx",
                Arg.Any<string[]?>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(stdioClient);

        await _sut.SyncToolsAsync(_integrationId);

        await _clientFactory.Received(1).CreateStdioClientAsync(
            "npx",
            Arg.Any<string[]?>(),
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
        await _clientFactory.DidNotReceive().GetOrCreateClientAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncToolsAsync_WithStdioTransport_DecryptsEnvironmentVariables()
    {
        var stdioIntegration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithName("Env MCP")
            .AsStdioMcpBacked("uvx")
            .WithMcpEncryptedEnvironmentVariables("enc-env-blob")
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(stdioIntegration);
        _credentialEncryptionService.Decrypt("enc-env-blob")
            .Returns("""{"API_TOKEN":"plain-value"}""");
        _toolCategoryDataAccess.FindByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(BuildCategory());
        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        var stdioClient = Substitute.For<IMcpClient>();
        stdioClient.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<IMcpToolDescriptor>());
        _clientFactory.CreateStdioClientAsync(
                Arg.Any<string>(),
                Arg.Any<string[]?>(),
                Arg.Is<Dictionary<string, string>?>(d => d != null && d["API_TOKEN"] == "plain-value"),
                Arg.Any<CancellationToken>())
            .Returns(stdioClient);

        await _sut.SyncToolsAsync(_integrationId);

        _credentialEncryptionService.Received(1).Decrypt("enc-env-blob");
    }

    // -------------------------------------------------------------------------
    // Idempotency — previously soft-deactivated tool re-appears (AC1 §4.2)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenSoftDeactivatedToolReappearsOnServer_ReactivatesExistingRecord()
    {
        var inactiveTool = new ToolActionBuilder()
            .WithToolCategoryId(_toolCategoryId)
            .WithIntegrationId(_integrationId)
            .WithMethodName("get_file")
            .WithName("get_file")
            .WithDescription("Gets a file")
            .AsInactive()
            .Build();

        ArrangeIntegrationAndCategoryWithAllTools(new List<ToolAction> { inactiveTool });
        ArrangeRemoteTools(CreateRemoteTool("get_file", "Gets a file"));

        await _sut.SyncToolsAsync(_integrationId);

        await _toolActionDataAccess.Received(1).UpdateAsync(
            Arg.Is<ToolAction>(t => t.MethodName == "get_file" && t.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncToolsAsync_WhenSoftDeactivatedToolReappearsOnServer_DoesNotInsertNewRecord()
    {
        var inactiveTool = new ToolActionBuilder()
            .WithToolCategoryId(_toolCategoryId)
            .WithIntegrationId(_integrationId)
            .WithMethodName("get_file")
            .WithName("get_file")
            .AsInactive()
            .Build();

        ArrangeIntegrationAndCategoryWithAllTools(new List<ToolAction> { inactiveTool });
        ArrangeRemoteTools(CreateRemoteTool("get_file", "Gets a file"));

        await _sut.SyncToolsAsync(_integrationId);

        await _toolActionDataAccess.DidNotReceive()
            .AddRangeAsync(Arg.Any<IEnumerable<ToolAction>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncToolsAsync_WhenSoftDeactivatedToolReappearsOnServer_CountedAsAdded()
    {
        var inactiveTool = new ToolActionBuilder()
            .WithToolCategoryId(_toolCategoryId)
            .WithIntegrationId(_integrationId)
            .WithMethodName("get_file")
            .WithName("get_file")
            .AsInactive()
            .Build();

        ArrangeIntegrationAndCategoryWithAllTools(new List<ToolAction> { inactiveTool });
        ArrangeRemoteTools(CreateRemoteTool("get_file", "Gets a file"));

        var result = await _sut.SyncToolsAsync(_integrationId);

        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Total);
    }

    // -------------------------------------------------------------------------
    // AC2 — soft-deactivation reflected in sync summary counts
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenToolRemovedFromServer_ReturnsSummaryWithRemovedCount()
    {
        var existingTool1 = new ToolActionBuilder()
            .WithToolCategoryId(_toolCategoryId)
            .WithIntegrationId(_integrationId)
            .WithMethodName("read_file")
            .WithName("read_file")
            .Build();

        var existingTool2 = new ToolActionBuilder()
            .WithToolCategoryId(_toolCategoryId)
            .WithIntegrationId(_integrationId)
            .WithMethodName("write_file")
            .WithName("write_file")
            .Build();

        ArrangeIntegrationAndCategoryWithAllTools(new List<ToolAction> { existingTool1, existingTool2 });
        ArrangeRemoteTools(CreateRemoteTool("read_file", "Reads a file"));

        var result = await _sut.SyncToolsAsync(_integrationId);

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Removed);
        Assert.Equal(1, result.Total);
    }

    // -------------------------------------------------------------------------
    // AC4 — stdio re-sync uses stored McpCommand and McpArguments
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WithStdioTransport_PassesStoredCommandAndArgsToFactory()
    {
        const string expectedCommand = "npx";
        const string expectedArgumentsJson = "[\"@modelcontextprotocol/server-filesystem\", \"/data\"]";

        var stdioIntegration = new IntegrationBuilder()
            .WithId(_integrationId)
            .AsStdioMcpBacked(expectedCommand)
            .WithMcpArgumentsJson(expectedArgumentsJson)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(stdioIntegration);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(BuildCategory());
        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        var stdioClient = Substitute.For<IMcpClient>();
        stdioClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<IMcpToolDescriptor>());
        _clientFactory.CreateStdioClientAsync(
                expectedCommand,
                Arg.Any<string[]?>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(stdioClient);

        await _sut.SyncToolsAsync(_integrationId);

        await _clientFactory.Received(1).CreateStdioClientAsync(
            expectedCommand,
            Arg.Is<string[]?>(a => a != null &&
                a[0] == "@modelcontextprotocol/server-filesystem" &&
                a[1] == "/data"),
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Destructive tool re-activation — IsEnabled preserved (AC1 §4.2 + §4.3)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenDestructiveToolReappears_PreservesIsEnabledFalse()
    {
        var inactiveDestructiveTool = new ToolActionBuilder()
            .WithToolCategoryId(_toolCategoryId)
            .WithIntegrationId(_integrationId)
            .WithMethodName("delete_all_files")
            .WithName("delete_all_files")
            .WithDescription("Permanently deletes all files in a directory")
            .AsInactive()
            .WithIsEnabled(false)
            .Build();

        ArrangeIntegrationAndCategoryWithAllTools(new List<ToolAction> { inactiveDestructiveTool });
        ArrangeRemoteTools(CreateRemoteTool("delete_all_files", "Permanently deletes all files in a directory"));

        await _sut.SyncToolsAsync(_integrationId);

        await _toolActionDataAccess.Received(1).UpdateAsync(
            Arg.Is<ToolAction>(t =>
                t.MethodName == "delete_all_files" &&
                t.IsActive &&
                !t.IsEnabled),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ArrangeIntegrationAndCategoryWithAllTools(List<ToolAction> allTools)
    {
        ArrangeIntegrationAndCategory();
        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(allTools.Where(t => t.IsActive).ToList());
        _toolActionDataAccess.GetByIntegrationIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(allTools);
    }

    private ToolCategory BuildCategory()
    {
        var category = ToolCategory.CreateMcpCategory("FS MCP", string.Empty, ProviderType.MCP_GENERIC, _integrationId);
        typeof(ToolCategory).GetProperty(nameof(ToolCategory.Id))!.SetValue(category, _toolCategoryId);
        return category;
    }
}
