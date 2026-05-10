using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Tools.DTOs;
using Orchestra.Domain.Exceptions;
using Orchestra.Domain.Interfaces;
using Orchestra.Domain.ValueObjects;

namespace Orchestra.Application.Tests.Tests.Tools;

public class McpToolSeedingServiceTests
{
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly ICredentialEncryptionService _credentialEncryptionService = Substitute.For<ICredentialEncryptionService>();
    private readonly IMcpToolDiscoveryService _mcpToolDiscoveryService = Substitute.For<IMcpToolDiscoveryService>();
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess = Substitute.For<IToolCategoryDataAccess>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
    private readonly IMcpToolSeedingService _sut;

    public McpToolSeedingServiceTests()
    {
        _sut = new Orchestra.Application.Tools.Services.McpToolSeedingService(
            _integrationDataAccess,
            _credentialEncryptionService,
            _mcpToolDiscoveryService,
            _toolCategoryDataAccess,
            _toolActionDataAccess);
    }

    // -------------------------------------------------------------------------
    // Scenario 1: Successfully discover and seed tools from Figma MCP server
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithValidMcpIntegration_CreatesToolCategoryAndActions()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Figma")
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", encryptedApiKey: "enc-key-123")
            .Build();

        var discoveredTools = new McpToolDiscoveryResult(
        [
            new DiscoveredMcpTool("get_design_context", "Reads design context", DangerLevel.Safe, null, true),
            new DiscoveredMcpTool("list_files", "Lists Figma files", DangerLevel.Safe, null, true)
        ]);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _credentialEncryptionService.Decrypt("enc-key-123")
            .Returns("plain-api-key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(
                "https://mcp.figma.com/sse",
                "API_KEY",
                "plain-api-key",
                Arg.Any<CancellationToken>())
            .Returns(discoveredTools);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((ToolCategory?)null);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        var result = await _sut.SeedToolsFromIntegrationAsync(integrationId);

        Assert.Equal(integrationId, result.IntegrationId);
        Assert.Equal("Figma", result.IntegrationName);
        Assert.Equal(2, result.TotalToolCount);
        Assert.Equal(2, result.SafeCount);
        Assert.Equal(0, result.DestructiveCount);
        Assert.All(result.Tools, t => Assert.True(t.IsEnabled));

        await _toolCategoryDataAccess.Received(1).AddAsync(
            Arg.Is<ToolCategory>(tc => tc.IntegrationId == integrationId && tc.Name == "Figma"),
            Arg.Any<CancellationToken>());

        await _toolActionDataAccess.Received(2).AddAsync(
            Arg.Is<ToolAction>(ta => ta.IsMcpTool && ta.IntegrationId == integrationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithValidMcpIntegration_SetsIsMcpToolTrue()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Figma")
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", "enc-key")
            .Build();

        var discoveredTools = new McpToolDiscoveryResult(
        [
            new DiscoveredMcpTool("get_design", "Gets design", DangerLevel.Safe, """{"type":"object"}""", true)
        ]);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _credentialEncryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(discoveredTools);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns((ToolCategory?)null);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        await _sut.SeedToolsFromIntegrationAsync(integrationId);

        await _toolActionDataAccess.Received(1).AddAsync(
            Arg.Is<ToolAction>(ta => ta.IsMcpTool && ta.McpToolSchema != null),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 2: Destructive tools flagged and disabled by default
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithDestructiveTool_SeedsWithIsEnabledFalse()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Figma")
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", "enc-key")
            .Build();

        var discoveredTools = new McpToolDiscoveryResult(
        [
            new DiscoveredMcpTool("use_figma", "Writes to Figma", DangerLevel.Destructive, null, false),
            new DiscoveredMcpTool("create_new_file", "Creates a file", DangerLevel.Destructive, null, false)
        ]);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _credentialEncryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(discoveredTools);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns((ToolCategory?)null);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        var result = await _sut.SeedToolsFromIntegrationAsync(integrationId);

        Assert.Equal(2, result.DestructiveCount);
        Assert.Equal(0, result.SafeCount);
        Assert.All(result.Tools, t => Assert.False(t.IsEnabled));

        await _toolActionDataAccess.Received(2).AddAsync(
            Arg.Is<ToolAction>(ta => ta.DangerLevel == DangerLevel.Destructive && !ta.IsEnabled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithDestructiveTool_IncludesInResultButIsEnabledFalse()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Figma")
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", "enc-key")
            .Build();

        var discoveredTools = new McpToolDiscoveryResult(
        [
            new DiscoveredMcpTool("use_figma", "Broad-write tool", DangerLevel.Destructive, null, false)
        ]);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _credentialEncryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(discoveredTools);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns((ToolCategory?)null);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        var result = await _sut.SeedToolsFromIntegrationAsync(integrationId);

        Assert.Single(result.Tools);
        Assert.Equal("use_figma", result.Tools[0].Name);
        Assert.False(result.Tools[0].IsEnabled);
        Assert.Equal("Destructive", result.Tools[0].DangerLevel);
    }

    // -------------------------------------------------------------------------
    // Scenario 3: Idempotent re-discovery
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WhenCategoryAlreadyExists_UsesExistingCategory()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Figma")
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", "enc-key")
            .Build();

        var existingCategory = new ToolCategoryBuilder()
            .WithName("Figma")
            .WithIntegrationId(integrationId)
            .Build();

        var discoveredTools = new McpToolDiscoveryResult(
        [
            new DiscoveredMcpTool("get_design_context", "Gets design", DangerLevel.Safe, null, true)
        ]);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _credentialEncryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(discoveredTools);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(existingCategory);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        var result = await _sut.SeedToolsFromIntegrationAsync(integrationId);

        await _toolCategoryDataAccess.DidNotReceive().AddAsync(Arg.Any<ToolCategory>(), Arg.Any<CancellationToken>());
        await _toolActionDataAccess.Received(1).AddAsync(
            Arg.Is<ToolAction>(ta => ta.ToolCategoryId == existingCategory.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_OnReSync_UpdatesExistingToolMcpToolSchema()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Figma")
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", "enc-key")
            .Build();

        var existingCategory = new ToolCategoryBuilder().WithIntegrationId(integrationId).Build();
        var existingToolAction = new ToolActionBuilder()
            .WithToolCategoryId(existingCategory.Id)
            .WithMethodName("get_design_context")
            .AsMcpTool(integrationId)
            .Build();

        const string updatedSchema = """{"type":"object","properties":{"nodeId":{"type":"string"}}}""";
        var discoveredTools = new McpToolDiscoveryResult(
        [
            new DiscoveredMcpTool("get_design_context", "Gets design context", DangerLevel.Safe, updatedSchema, true)
        ]);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _credentialEncryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(discoveredTools);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(existingCategory);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(existingCategory.Id, "get_design_context", Arg.Any<CancellationToken>())
            .Returns(existingToolAction);

        await _sut.SeedToolsFromIntegrationAsync(integrationId);

        await _toolActionDataAccess.Received(1).UpdateAsync(
            Arg.Is<ToolAction>(ta => ta.McpToolSchema == updatedSchema),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WhenToolAlreadyExists_UpdatesInsteadOfCreating()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Figma")
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", "enc-key")
            .Build();

        var existingCategory = new ToolCategoryBuilder().WithIntegrationId(integrationId).Build();
        var existingToolAction = new ToolActionBuilder()
            .WithToolCategoryId(existingCategory.Id)
            .WithMethodName("get_design_context")
            .AsMcpTool(integrationId)
            .Build();

        var discoveredTools = new McpToolDiscoveryResult(
        [
            new DiscoveredMcpTool("get_design_context", "Updated description", DangerLevel.Safe, """{"updated":true}""", true)
        ]);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _credentialEncryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(discoveredTools);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(existingCategory);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(existingCategory.Id, "get_design_context", Arg.Any<CancellationToken>())
            .Returns(existingToolAction);

        await _sut.SeedToolsFromIntegrationAsync(integrationId);

        await _toolActionDataAccess.DidNotReceive().AddAsync(Arg.Any<ToolAction>(), Arg.Any<CancellationToken>());
        await _toolActionDataAccess.Received(1).UpdateAsync(
            Arg.Is<ToolAction>(ta => ta.MethodName == "get_design_context"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_OnReSync_NoDuplicateToolActionsCreated()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Figma")
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", "enc-key")
            .Build();

        var existingCategory = new ToolCategoryBuilder().WithIntegrationId(integrationId).Build();
        var existingTool = new ToolActionBuilder()
            .WithToolCategoryId(existingCategory.Id)
            .WithMethodName("list_files")
            .AsMcpTool(integrationId)
            .Build();

        var discoveredTools = new McpToolDiscoveryResult(
        [
            new DiscoveredMcpTool("list_files", "Lists files", DangerLevel.Safe, null, true),
            new DiscoveredMcpTool("create_new_file", "Creates file", DangerLevel.Destructive, null, false)
        ]);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _credentialEncryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(discoveredTools);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(existingCategory);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(existingCategory.Id, "list_files", Arg.Any<CancellationToken>())
            .Returns(existingTool);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(existingCategory.Id, "create_new_file", Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        var result = await _sut.SeedToolsFromIntegrationAsync(integrationId);

        await _toolActionDataAccess.Received(1).AddAsync(Arg.Any<ToolAction>(), Arg.Any<CancellationToken>());
        await _toolActionDataAccess.Received(1).UpdateAsync(Arg.Any<ToolAction>(), Arg.Any<CancellationToken>());
        Assert.Equal(2, result.TotalToolCount);
    }

    // -------------------------------------------------------------------------
    // Scenario 4: MCP server connection failure during discovery
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WhenMcpServerDown_ThrowsMcpConnectionException()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", "enc-key")
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _credentialEncryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpConnectionException(McpConnectionErrorCode.MCP_UNREACHABLE, "Server unreachable."));

        await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.SeedToolsFromIntegrationAsync(integrationId));
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WhenMcpServerDown_NoToolRecordsCreatedOrModified()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", "enc-key")
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _credentialEncryptionService.Decrypt(Arg.Any<string>()).Returns("key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpConnectionException(McpConnectionErrorCode.MCP_UNREACHABLE, "Server unreachable."));

        await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.SeedToolsFromIntegrationAsync(integrationId));

        await _toolCategoryDataAccess.DidNotReceive().AddAsync(Arg.Any<ToolCategory>(), Arg.Any<CancellationToken>());
        await _toolActionDataAccess.DidNotReceive().AddAsync(Arg.Any<ToolAction>(), Arg.Any<CancellationToken>());
        await _toolActionDataAccess.DidNotReceive().UpdateAsync(Arg.Any<ToolAction>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WhenIntegrationNotFound_ThrowsIntegrationNotFoundException()
    {
        var integrationId = Guid.NewGuid();
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((Integration?)null);

        await Assert.ThrowsAsync<IntegrationNotFoundException>(
            () => _sut.SeedToolsFromIntegrationAsync(integrationId));
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WhenIntegrationIsNotMcpBacked_ThrowsInvalidOperationException()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SeedToolsFromIntegrationAsync(integrationId));
    }

    // -------------------------------------------------------------------------
    // Scenario 2: Successfully discover and seed tools via stdio transport
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithStdioIntegration_CallsDiscoverStdioToolsAsync()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Filesystem MCP")
            .AsStdioMcpBacked("npx")
            .WithMcpArgumentsJson("""["@modelcontextprotocol/server-filesystem","/home"]""")
            .WithMcpEncryptedEnvironmentVariables("enc-env-blob")
            .Build();

        var discoveredTools = new McpToolDiscoveryResult(
        [
            new DiscoveredMcpTool("list_files", "Lists files in a directory", DangerLevel.Safe, null, true),
            new DiscoveredMcpTool("read_file", "Reads a file", DangerLevel.Safe, null, true)
        ]);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _credentialEncryptionService.Decrypt("enc-env-blob")
            .Returns("""{"API_TOKEN":"plain-token"}""");
        _mcpToolDiscoveryService.DiscoverStdioToolsAsync(
                "npx",
                Arg.Any<string[]?>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(discoveredTools);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((ToolCategory?)null);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        var result = await _sut.SeedToolsFromIntegrationAsync(integrationId);

        Assert.Equal(integrationId, result.IntegrationId);
        Assert.Equal(2, result.TotalToolCount);
        Assert.Equal(2, result.SafeCount);

        await _mcpToolDiscoveryService.Received(1).DiscoverStdioToolsAsync(
            "npx",
            Arg.Any<string[]?>(),
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<CancellationToken>());

        await _mcpToolDiscoveryService.DidNotReceive().DiscoverToolsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithStdioIntegration_DecryptsEnvironmentVariables()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Git MCP")
            .AsStdioMcpBacked("uvx")
            .WithMcpEncryptedEnvironmentVariables("enc-env-123")
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _credentialEncryptionService.Decrypt("enc-env-123")
            .Returns("""{"GH_TOKEN":"ghp_secret"}""");
        _mcpToolDiscoveryService.DiscoverStdioToolsAsync(
                Arg.Any<string>(), Arg.Any<string[]?>(),
                Arg.Is<Dictionary<string, string>?>(d => d != null && d["GH_TOKEN"] == "ghp_secret"),
                Arg.Any<CancellationToken>())
            .Returns(new McpToolDiscoveryResult([
                new DiscoveredMcpTool("list_repos", "Lists repos", DangerLevel.Safe, null, true)
            ]));
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((ToolCategory?)null);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        await _sut.SeedToolsFromIntegrationAsync(integrationId);

        _credentialEncryptionService.Received(1).Decrypt("enc-env-123");
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithStdioIntegrationNoEnvVars_PassesNullEnvironmentVariables()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("No-Env MCP")
            .AsStdioMcpBacked("python")
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.DiscoverStdioToolsAsync(
                "python",
                Arg.Any<string[]?>(),
                Arg.Is<Dictionary<string, string>?>(d => d == null || d.Count == 0),
                Arg.Any<CancellationToken>())
            .Returns(new McpToolDiscoveryResult([
                new DiscoveredMcpTool("run_query", "Runs a query", DangerLevel.Safe, null, true)
            ]));
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((ToolCategory?)null);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        var result = await _sut.SeedToolsFromIntegrationAsync(integrationId);

        Assert.Equal(1, result.TotalToolCount);
        _credentialEncryptionService.DidNotReceive().Decrypt(Arg.Any<string>());
    }

    // -------------------------------------------------------------------------
    // Scenario 3: Destructive tools flagged and disabled by default (STDIO)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithStdioIntegration_DestructiveToolSeededAsDisabled()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("DB MCP")
            .AsStdioMcpBacked("npx")
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.DiscoverStdioToolsAsync(
                Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new McpToolDiscoveryResult([
                new DiscoveredMcpTool("list_tables", "Lists all tables", DangerLevel.Safe, null, true),
                new DiscoveredMcpTool("delete_record", "Deletes a record", DangerLevel.Destructive, null, false)
            ]));
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((ToolCategory?)null);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ToolAction?)null);

        var result = await _sut.SeedToolsFromIntegrationAsync(integrationId);

        Assert.Equal(1, result.SafeCount);
        Assert.Equal(1, result.DestructiveCount);

        var destructiveTool = result.Tools.Single(t => t.DangerLevel == DangerLevel.Destructive.ToString());
        Assert.False(destructiveTool.IsEnabled);

        var safeTool = result.Tools.Single(t => t.DangerLevel == DangerLevel.Safe.ToString());
        Assert.True(safeTool.IsEnabled);
    }

    // -------------------------------------------------------------------------
    // Scenario 4: Idempotent re-discovery (STDIO)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithStdioIntegration_WhenToolAlreadyExists_UpdatesNotCreates()
    {
        var integrationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("FS MCP")
            .AsStdioMcpBacked("npx")
            .Build();

        var existingCategory = ToolCategory.CreateMcpCategory(
            "FS MCP", "Tools from FS MCP MCP server", ProviderType.MCP_GENERIC, integrationId);
        typeof(ToolCategory).GetProperty(nameof(ToolCategory.Id))!.SetValue(existingCategory, categoryId);

        var existingAction = ToolAction.CreateMcpTool(
            categoryId, integrationId, "list_files", "Old description",
            "list_files", DangerLevel.Safe, null, true);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.DiscoverStdioToolsAsync(
                Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new McpToolDiscoveryResult([
                new DiscoveredMcpTool("list_files", "Updated description", DangerLevel.Safe, null, true)
            ]));
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(existingCategory);
        _toolActionDataAccess.FindByToolCategoryIdAndMethodNameAsync(
                categoryId, "list_files", Arg.Any<CancellationToken>())
            .Returns(existingAction);

        await _sut.SeedToolsFromIntegrationAsync(integrationId);

        await _toolActionDataAccess.Received(1).UpdateAsync(existingAction, Arg.Any<CancellationToken>());
        await _toolActionDataAccess.DidNotReceive().AddAsync(Arg.Any<ToolAction>(), Arg.Any<CancellationToken>());
        await _toolCategoryDataAccess.DidNotReceive().AddAsync(Arg.Any<ToolCategory>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 5: STDIO process connection failure during discovery
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithStdioIntegration_WhenProcessLaunchExceptionThrown_PropagatesException()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Bad Process MCP")
            .AsStdioMcpBacked("nonexistent-cmd")
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.DiscoverStdioToolsAsync(
                Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProcessLaunchException("nonexistent-cmd"));

        await Assert.ThrowsAsync<ProcessLaunchException>(() =>
            _sut.SeedToolsFromIntegrationAsync(integrationId));

        await _toolCategoryDataAccess.DidNotReceive().AddAsync(Arg.Any<ToolCategory>(), Arg.Any<CancellationToken>());
        await _toolActionDataAccess.DidNotReceive().AddAsync(Arg.Any<ToolAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithStdioIntegration_WhenDiscoveryTimesOut_PropagatesDiscoveryTimeoutException()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithName("Slow MCP")
            .AsStdioMcpBacked("slow-cmd")
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.DiscoverStdioToolsAsync(
                Arg.Any<string>(), Arg.Any<string[]?>(), Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new DiscoveryTimeoutException());

        await Assert.ThrowsAsync<DiscoveryTimeoutException>(() =>
            _sut.SeedToolsFromIntegrationAsync(integrationId));

        await _toolCategoryDataAccess.DidNotReceive().AddAsync(Arg.Any<ToolCategory>(), Arg.Any<CancellationToken>());
        await _toolActionDataAccess.DidNotReceive().AddAsync(Arg.Any<ToolAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_DecryptedApiKeyIsNeverLogged()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .AsMcpBacked("https://mcp.figma.com/sse", "API_KEY", "enc-key")
            .Build();

        var discoveredTools = new McpToolDiscoveryResult([]);

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _credentialEncryptionService.Decrypt("enc-key").Returns("secret-plain-key");
        _mcpToolDiscoveryService.DiscoverToolsAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                "secret-plain-key",
                Arg.Any<CancellationToken>())
            .Returns(discoveredTools);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((ToolCategory?)null);

        await _sut.SeedToolsFromIntegrationAsync(integrationId);

        await _mcpToolDiscoveryService.Received(1).DiscoverToolsAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            "secret-plain-key",
            Arg.Any<CancellationToken>());
    }
}
