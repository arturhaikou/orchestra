using Microsoft.EntityFrameworkCore;
using Orchestra.Infrastructure.Agents;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="AgentMcpToolDataAccess"/> covering all four
/// un-implemented persistence methods: GetByAgentIdAsync, GetByAgentAndServerIdAsync,
/// ReplaceForAgentAndServerAsync, and DeleteAllForAgentAsync.
/// Uses EF Core InMemory provider — each test gets a fresh, isolated database.
/// </summary>
public class AgentMcpToolDataAccessTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // -------------------------------------------------------------------------
    // GetByAgentIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByAgentIdAsync_WithMultipleRecords_ReturnsOnlyMatchingAgent()
    {
        await using var db = CreateContext();
        var agentId = Guid.NewGuid();
        var otherAgentId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        db.AgentMcpTools.AddRange(
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).WithToolName("tool_a").Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).WithToolName("tool_b").Build(),
            new AgentMcpToolBuilder().WithAgentId(otherAgentId).WithMcpServerId(serverId).WithToolName("tool_c").Build()
        );
        await db.SaveChangesAsync();

        var sut = new AgentMcpToolDataAccess(db);
        var result = await sut.GetByAgentIdAsync(agentId);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(agentId, t.AgentId));
    }

    [Fact]
    public async Task GetByAgentIdAsync_WithNoRecords_ReturnsEmptyList()
    {
        await using var db = CreateContext();
        var sut = new AgentMcpToolDataAccess(db);

        var result = await sut.GetByAgentIdAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // GetByAgentAndServerIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByAgentAndServerIdAsync_ReturnsOnlyMatchingAgentAndServerPair()
    {
        await using var db = CreateContext();
        var agentId = Guid.NewGuid();
        var serverId1 = Guid.NewGuid();
        var serverId2 = Guid.NewGuid();

        db.AgentMcpTools.AddRange(
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId1).WithToolName("tool_a").Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId1).WithToolName("tool_b").Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId2).WithToolName("tool_c").Build()
        );
        await db.SaveChangesAsync();

        var sut = new AgentMcpToolDataAccess(db);
        var result = await sut.GetByAgentAndServerIdAsync(agentId, serverId1);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(serverId1, t.McpServerId));
    }

    [Fact]
    public async Task GetByAgentAndServerIdAsync_WithNoMatchingRecords_ReturnsEmptyList()
    {
        await using var db = CreateContext();
        var sut = new AgentMcpToolDataAccess(db);

        var result = await sut.GetByAgentAndServerIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // ReplaceForAgentAndServerAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReplaceForAgentAndServerAsync_WithExistingRecords_ReplacesAllWithNewOnes()
    {
        await using var db = CreateContext();
        var agentId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        db.AgentMcpTools.AddRange(
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).WithToolName("old_1").Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).WithToolName("old_2").Build()
        );
        await db.SaveChangesAsync();

        var replacements = new List<AgentMcpTool>
        {
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).WithToolName("new_1").Build()
        };

        var sut = new AgentMcpToolDataAccess(db);
        await sut.ReplaceForAgentAndServerAsync(agentId, serverId, replacements);

        var remaining = await sut.GetByAgentAndServerIdAsync(agentId, serverId);
        Assert.Single(remaining);
        Assert.Equal("new_1", remaining[0].ToolName);
    }

    [Fact]
    public async Task ReplaceForAgentAndServerAsync_WithEmptyReplacements_DeletesAllExistingRecords()
    {
        await using var db = CreateContext();
        var agentId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        db.AgentMcpTools.AddRange(
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).Build()
        );
        await db.SaveChangesAsync();

        var sut = new AgentMcpToolDataAccess(db);
        await sut.ReplaceForAgentAndServerAsync(agentId, serverId, []);

        var remaining = await sut.GetByAgentAndServerIdAsync(agentId, serverId);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task ReplaceForAgentAndServerAsync_DoesNotAffectToolsForOtherServers()
    {
        await using var db = CreateContext();
        var agentId = Guid.NewGuid();
        var serverId1 = Guid.NewGuid();
        var serverId2 = Guid.NewGuid();

        db.AgentMcpTools.AddRange(
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId1).WithToolName("s1_tool").Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId2).WithToolName("s2_tool").Build()
        );
        await db.SaveChangesAsync();

        var replacement = new AgentMcpToolBuilder()
            .WithAgentId(agentId)
            .WithMcpServerId(serverId1)
            .WithToolName("s1_new")
            .Build();

        var sut = new AgentMcpToolDataAccess(db);
        await sut.ReplaceForAgentAndServerAsync(agentId, serverId1, [replacement]);

        var server2Tools = await sut.GetByAgentAndServerIdAsync(agentId, serverId2);
        Assert.Single(server2Tools);
        Assert.Equal("s2_tool", server2Tools[0].ToolName);
    }

    [Fact]
    public async Task ReplaceForAgentAndServerAsync_WithNoExistingRecords_InsertsReplacements()
    {
        await using var db = CreateContext();
        var agentId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        var newTool = new AgentMcpToolBuilder()
            .WithAgentId(agentId)
            .WithMcpServerId(serverId)
            .WithToolName("brand_new")
            .Build();

        var sut = new AgentMcpToolDataAccess(db);
        await sut.ReplaceForAgentAndServerAsync(agentId, serverId, [newTool]);

        var result = await sut.GetByAgentAndServerIdAsync(agentId, serverId);
        Assert.Single(result);
        Assert.Equal("brand_new", result[0].ToolName);
    }

    // -------------------------------------------------------------------------
    // DeleteAllForAgentAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAllForAgentAsync_RemovesAllRecordsForTargetAgent()
    {
        await using var db = CreateContext();
        var agentId = Guid.NewGuid();
        var otherAgentId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        db.AgentMcpTools.AddRange(
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).Build(),
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).Build(),
            new AgentMcpToolBuilder().WithAgentId(otherAgentId).WithMcpServerId(serverId).Build(),
            new AgentMcpToolBuilder().WithAgentId(otherAgentId).WithMcpServerId(serverId).Build()
        );
        await db.SaveChangesAsync();

        var sut = new AgentMcpToolDataAccess(db);
        await sut.DeleteAllForAgentAsync(agentId);

        var agentTools = await sut.GetByAgentIdAsync(agentId);
        var otherAgentTools = await sut.GetByAgentIdAsync(otherAgentId);

        Assert.Empty(agentTools);
        Assert.Equal(2, otherAgentTools.Count);
    }

    [Fact]
    public async Task DeleteAllForAgentAsync_WithNoRecords_CompletesWithoutError()
    {
        await using var db = CreateContext();
        var sut = new AgentMcpToolDataAccess(db);

        var exception = await Record.ExceptionAsync(() => sut.DeleteAllForAgentAsync(Guid.NewGuid()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task DeleteAllForAgentAsync_DoesNotAffectOtherAgents()
    {
        await using var db = CreateContext();
        var agentId = Guid.NewGuid();
        var survivingAgentId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        db.AgentMcpTools.AddRange(
            new AgentMcpToolBuilder().WithAgentId(agentId).WithMcpServerId(serverId).Build(),
            new AgentMcpToolBuilder().WithAgentId(survivingAgentId).WithMcpServerId(serverId).WithToolName("survivor").Build()
        );
        await db.SaveChangesAsync();

        var sut = new AgentMcpToolDataAccess(db);
        await sut.DeleteAllForAgentAsync(agentId);

        var surviving = await sut.GetByAgentIdAsync(survivingAgentId);
        Assert.Single(surviving);
        Assert.Equal("survivor", surviving[0].ToolName);
    }
}
