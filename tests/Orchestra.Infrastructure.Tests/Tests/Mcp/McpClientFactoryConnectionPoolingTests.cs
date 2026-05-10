using Microsoft.Extensions.Logging.Abstractions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Infrastructure.Mcp;

namespace Orchestra.Infrastructure.Tests.Tests.Mcp;

public class McpClientFactoryConnectionPoolingTests
{
    private readonly McpClientFactory _sut;

    public McpClientFactoryConnectionPoolingTests()
    {
        _sut = new McpClientFactory(NullLoggerFactory.Instance, NullLogger<McpClientFactory>.Instance);
    }

    // -------------------------------------------------------------------------
    // Connection pooling — same integrationId returns same client
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrCreateClientAsync_CalledTwiceWithSameIntegrationId_ReturnsSameClientInstance()
    {
        var integrationId = Guid.NewGuid();
        const string endpoint = "https://mcp.example.com";

        var first = await _sut.GetOrCreateClientAsync(integrationId, endpoint, null);
        var second = await _sut.GetOrCreateClientAsync(integrationId, endpoint, null);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetOrCreateClientAsync_CalledWithDifferentIntegrationIds_ReturnsDifferentClients()
    {
        var integrationIdA = Guid.NewGuid();
        var integrationIdB = Guid.NewGuid();
        const string endpoint = "https://mcp.example.com";

        var clientA = await _sut.GetOrCreateClientAsync(integrationIdA, endpoint, null);
        var clientB = await _sut.GetOrCreateClientAsync(integrationIdB, endpoint, null);

        Assert.NotSame(clientA, clientB);
    }

    [Fact]
    public async Task GetOrCreateClientAsync_CalledFiveTimesWithSameIntegrationId_CreatesOnlyOneConnection()
    {
        var integrationId = Guid.NewGuid();
        const string endpoint = "https://mcp.example.com";
        var clients = new List<IMcpClient>();

        for (var i = 0; i < 5; i++)
            clients.Add(await _sut.GetOrCreateClientAsync(integrationId, endpoint, null));

        Assert.True(clients.Distinct().Count() == 1, "Expected all 5 calls to return the same client instance");
    }

    // -------------------------------------------------------------------------
    // HTTPS enforcement
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrCreateClientAsync_WithHttpEndpoint_ThrowsArgumentException()
    {
        var integrationId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetOrCreateClientAsync(integrationId, "http://insecure.example.com", null));
    }

    [Theory]
    [InlineData("ftp://mcp.example.com")]
    [InlineData("ws://mcp.example.com")]
    [InlineData("http://mcp.example.com")]
    public async Task GetOrCreateClientAsync_WithNonHttpsScheme_ThrowsArgumentException(string endpoint)
    {
        var integrationId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetOrCreateClientAsync(integrationId, endpoint, null));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_WithValidHttpsEndpoint_DoesNotThrow()
    {
        var integrationId = Guid.NewGuid();

        var exception = await Record.ExceptionAsync(
            () => _sut.GetOrCreateClientAsync(integrationId, "https://mcp.example.com", null));

        Assert.Null(exception);
    }

    // -------------------------------------------------------------------------
    // Credential injection (API key passed as Bearer token)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrCreateClientAsync_WithApiKey_ReturnsClientWithoutExposedException()
    {
        // Verify that providing an API key does not throw and returns a client
        // (The actual header injection is internal — we verify non-null return)
        var integrationId = Guid.NewGuid();
        const string endpoint = "https://mcp.example.com";
        const string apiKey = "secret-api-key-12345";

        var client = await _sut.GetOrCreateClientAsync(integrationId, endpoint, apiKey);

        Assert.NotNull(client);
    }

    [Fact]
    public async Task GetOrCreateClientAsync_WithNullApiKey_ReturnsClient()
    {
        var integrationId = Guid.NewGuid();

        var client = await _sut.GetOrCreateClientAsync(integrationId, "https://mcp.example.com", null);

        Assert.NotNull(client);
    }

    // -------------------------------------------------------------------------
    // Disposal — DisposeAsync cleans up all cached clients
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_AfterCreatingMultipleClients_DoesNotThrow()
    {
        await _sut.GetOrCreateClientAsync(Guid.NewGuid(), "https://mcp.example.com", null);
        await _sut.GetOrCreateClientAsync(Guid.NewGuid(), "https://mcp2.example.com", null);

        var exception = await Record.ExceptionAsync(() => _sut.DisposeAsync().AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_CalledOnEmptyFactory_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() => _sut.DisposeAsync().AsTask());

        Assert.Null(exception);
    }
}
