using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Tools.Services;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Application.Tests.Tests.Tools;

public class McpToolSeedingServiceFr006Tests
{
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly ICredentialEncryptionService _encryptionService = Substitute.For<ICredentialEncryptionService>();
    private readonly IMcpToolDiscoveryService _discoveryService = Substitute.For<IMcpToolDiscoveryService>();
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess = Substitute.For<IToolCategoryDataAccess>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();

    private McpToolSeedingService CreateSut() => new(
        _integrationDataAccess,
        _encryptionService,
        _discoveryService,
        _toolCategoryDataAccess,
        _toolActionDataAccess);

    // ---------------------------------------------------------------
    // Scenario 5: Discover-tools flow rejects MCP_GENERIC
    // ---------------------------------------------------------------

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WithMcpGenericIntegration_ThrowsArgumentExceptionWithExpectedMessage()
    {
        var integrationId = Guid.NewGuid();

        var mcpIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .AsMcpBacked()
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(mcpIntegration);

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.SeedToolsFromIntegrationAsync(integrationId));

        Assert.Contains("MCP servers must be managed through the MCP Server settings", ex.Message);
    }

    [Fact]
    public async Task SeedToolsFromIntegrationAsync_WhenIntegrationNotFound_ThrowsIntegrationNotFoundException()
    {
        var integrationId = Guid.NewGuid();
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((Integration?)null);

        var sut = CreateSut();

        await Assert.ThrowsAsync<IntegrationNotFoundException>(
            () => sut.SeedToolsFromIntegrationAsync(integrationId));
    }
}
