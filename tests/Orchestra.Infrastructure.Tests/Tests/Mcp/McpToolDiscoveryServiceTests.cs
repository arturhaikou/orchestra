using Microsoft.Extensions.AI;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Exceptions;
using Orchestra.Infrastructure.Mcp;

namespace Orchestra.Infrastructure.Tests.Tests.Mcp;

public class McpToolDiscoveryServiceTests
{
    private readonly IMcpClientFactory _clientFactory;
    private readonly IMcpClient _mcpClient;
    private readonly McpToolDiscoveryService _sut;

    public McpToolDiscoveryServiceTests()
    {
        _clientFactory = Substitute.For<IMcpClientFactory>();
        _mcpClient = Substitute.For<IMcpClient>();

        _clientFactory.CreateClientAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_mcpClient);

        _sut = new McpToolDiscoveryService(_clientFactory);
    }

    // -------------------------------------------------------------------------
    // Danger Level Classification — SAFE
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("get_file_content", null)]
    [InlineData("list_pages", null)]
    [InlineData("fetch_comments", "Returns all comments")]
    [InlineData("read_variable", null)]
    public async Task DiscoverToolsAsync_WithReadOnlyTool_ClassifiesAsSafe(string toolName, string? description)
    {
        ArrangeSingleTool(toolName, description);

        var result = await _sut.DiscoverToolsAsync("https://mcp.example.com", "NONE", null);

        Assert.Single(result.Tools);
        Assert.Equal(DangerLevel.Safe, result.Tools[0].DangerLevel);
        Assert.True(result.Tools[0].Enabled);
    }

    // -------------------------------------------------------------------------
    // Danger Level Classification — MODERATE
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("create_component", null)]
    [InlineData("update_styles", null)]
    [InlineData("post_comment", "Posts a comment to a frame")]
    [InlineData("set_variable", null)]
    [InlineData("edit_layer", null)]
    [InlineData("add_annotation", null)]
    [InlineData("insert_node", null)]
    [InlineData("write_styles", null)]
    public async Task DiscoverToolsAsync_WithMutateTool_ClassifiesAsModerate(string toolName, string? description)
    {
        ArrangeSingleTool(toolName, description);

        var result = await _sut.DiscoverToolsAsync("https://mcp.example.com", "NONE", null);

        Assert.Single(result.Tools);
        Assert.Equal(DangerLevel.Moderate, result.Tools[0].DangerLevel);
        Assert.True(result.Tools[0].Enabled);
    }

    // -------------------------------------------------------------------------
    // Danger Level Classification — DESTRUCTIVE
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("delete_component", null)]
    [InlineData("remove_page", null)]
    [InlineData("destroy_layer", null)]
    [InlineData("purge_cache", "Removes all cached assets")]
    [InlineData("erase_history", null)]
    [InlineData("drop_frame", null)]
    [InlineData("truncate_feed", null)]
    public async Task DiscoverToolsAsync_WithDestructiveTool_ClassifiesAsDestructiveAndDisabled(string toolName, string? description)
    {
        ArrangeSingleTool(toolName, description);

        var result = await _sut.DiscoverToolsAsync("https://mcp.example.com", "NONE", null);

        Assert.Single(result.Tools);
        Assert.Equal(DangerLevel.Destructive, result.Tools[0].DangerLevel);
        Assert.False(result.Tools[0].Enabled);
    }

    [Fact]
    public async Task DiscoverToolsAsync_WithDestructiveKeywordInDescription_ClassifiesAsDestructive()
    {
        ArrangeSingleTool("run_cleanup", "Permanently deletes unused assets from the project");

        var result = await _sut.DiscoverToolsAsync("https://mcp.example.com", "NONE", null);

        Assert.Equal(DangerLevel.Destructive, result.Tools[0].DangerLevel);
    }

    [Fact]
    public async Task DiscoverToolsAsync_WithDestructiveTakesPrecedenceOverModerate_ClassifiesAsDestructive()
    {
        ArrangeSingleTool("create_and_delete_node", null);

        var result = await _sut.DiscoverToolsAsync("https://mcp.example.com", "NONE", null);

        Assert.Equal(DangerLevel.Destructive, result.Tools[0].DangerLevel);
    }

    // -------------------------------------------------------------------------
    // Multi-tool discovery
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DiscoverToolsAsync_WithMixedTools_ReturnsMixedDangerLevels()
    {
        ArrangeMultipleTools([
            ("get_file", null),
            ("update_styles", null),
            ("delete_layer", null)
        ]);

        var result = await _sut.DiscoverToolsAsync("https://mcp.example.com", "API_KEY", "key");

        Assert.Equal(3, result.ToolCount);
        Assert.Contains(result.Tools, t => t.DangerLevel == DangerLevel.Safe);
        Assert.Contains(result.Tools, t => t.DangerLevel == DangerLevel.Moderate);
        Assert.Contains(result.Tools, t => t.DangerLevel == DangerLevel.Destructive);
    }

    // -------------------------------------------------------------------------
    // Error scenarios
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DiscoverToolsAsync_WhenServerUnreachable_ThrowsMcpConnectionExceptionWithUnreachableCode()
    {
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var ex = await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.DiscoverToolsAsync("https://mcp.example.com", "NONE", null));

        Assert.Equal(McpConnectionErrorCode.MCP_UNREACHABLE, ex.ErrorCode);
    }

    [Fact]
    public async Task DiscoverToolsAsync_WhenServerReturns401_ThrowsMcpConnectionExceptionWithAuthFailedCode()
    {
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("401 Unauthorized"));

        var ex = await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.DiscoverToolsAsync("https://mcp.example.com", "API_KEY", "bad-key"));

        Assert.Equal(McpConnectionErrorCode.MCP_AUTH_FAILED, ex.ErrorCode);
    }

    [Fact]
    public async Task DiscoverToolsAsync_WhenCancellationTokenFires_ThrowsMcpConnectionExceptionWithTimeoutCode()
    {
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var ex = await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.DiscoverToolsAsync("https://mcp.example.com", "NONE", null));

        Assert.Equal(McpConnectionErrorCode.MCP_TIMEOUT, ex.ErrorCode);
    }

    [Fact]
    public async Task DiscoverToolsAsync_MapsToolNameToMethodName()
    {
        ArrangeSingleTool("get_file_content", "Gets a file");

        var result = await _sut.DiscoverToolsAsync("https://mcp.example.com", "NONE", null);

        Assert.Equal("get_file_content", result.Tools[0].Name);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ArrangeSingleTool(string name, string? description)
    {
        var tool = new FakeMcpTool(name, description);
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { tool });
    }

    private void ArrangeMultipleTools(IEnumerable<(string Name, string? Description)> tools)
    {
        var fakeTools = tools.Select(t => new FakeMcpTool(t.Name, t.Description)).ToArray();
        _mcpClient.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(fakeTools);
    }
}

internal record FakeMcpTool(string Name, string? Description) : Orchestra.Application.Common.Interfaces.IMcpToolDescriptor
{
    public AIFunction AsAIFunction()
    {
        Func<Task> stub = () => Task.CompletedTask;
        return AIFunctionFactory.Create(stub, Name, Description ?? string.Empty);
    }
}
