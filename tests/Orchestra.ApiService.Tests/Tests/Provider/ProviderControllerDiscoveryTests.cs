using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.ApiService.Controllers;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workspaces.DTOs;

namespace Orchestra.ApiService.Tests.Tests.Provider;

public class ProviderControllerDiscoveryTests
{
    private readonly IAzureOpenAIModelDiscoveryService _mockDiscoveryService;
    private readonly IOllamaModelDiscoveryService _mockOllamaDiscoveryService;
    private readonly ProviderController _controller;

    private const string ValidEndpoint = "https://my-resource.openai.azure.com";
    private const string ValidApiKey = "test-api-key-value";

    public ProviderControllerDiscoveryTests()
    {
        _mockDiscoveryService = Substitute.For<IAzureOpenAIModelDiscoveryService>();
        _mockOllamaDiscoveryService = Substitute.For<IOllamaModelDiscoveryService>();
        _controller = new ProviderController(_mockDiscoveryService, _mockOllamaDiscoveryService);
    }

    // -----------------------------------------------------------------------
    // Scenario 1: Valid credentials return model list
    // FR: POST /v1/provider/azure/models with valid endpoint and apiKey
    // Expected: 200 OK with a JSON array of deployment name strings
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DiscoverAzureModels_WhenCredentialsAreValid_Returns200WithModelList()
    {
        // Arrange
        var expectedModels = new List<string> { "gpt-4o", "gpt-4-turbo" }.AsReadOnly();

        _mockDiscoveryService
            .DiscoverModelsAsync(ValidEndpoint, ValidApiKey, Arg.Any<CancellationToken>())
            .Returns(expectedModels);

        var request = new DiscoverAzureModelsRequest
        {
            Endpoint = ValidEndpoint,
            ApiKey = ValidApiKey
        };

        // Act
        var result = await _controller.DiscoverAzureModels(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AIModelsResponse>(okResult.Value);
        Assert.Equal(expectedModels, response.Models);
    }

    // -----------------------------------------------------------------------
    // Scenario 1b: Valid credentials but no models deployed — still 200 OK
    // FR: An empty list is valid (workspace's Azure resource has no deployed models)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DiscoverAzureModels_WhenNoModelsDeployed_Returns200WithEmptyList()
    {
        // Arrange
        _mockDiscoveryService
            .DiscoverModelsAsync(ValidEndpoint, ValidApiKey, Arg.Any<CancellationToken>())
            .Returns(new List<string>().AsReadOnly());

        var request = new DiscoverAzureModelsRequest
        {
            Endpoint = ValidEndpoint,
            ApiKey = ValidApiKey
        };

        // Act
        var result = await _controller.DiscoverAzureModels(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AIModelsResponse>(okResult.Value);
        Assert.Empty(response.Models);
    }

    // -----------------------------------------------------------------------
    // Scenario 2: Invalid Azure credentials — mapped to 400 Bad Request
    // FR: "If DiscoverModelsAsync cannot reach Azure or credentials are rejected,
    //      the endpoint returns 400 Bad Request with an informative error message.
    //      The supplied API key must NOT appear in the response body."
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DiscoverAzureModels_WhenAzureRejectsCredentials_Returns400WithSanitisedMessage()
    {
        // Arrange
        _mockDiscoveryService
            .DiscoverModelsAsync(ValidEndpoint, ValidApiKey, Arg.Any<CancellationToken>())
.ThrowsAsync(new AIProviderCommunicationException(
                "Azure OpenAI returned HTTP 401 when listing models."));

        var request = new DiscoverAzureModelsRequest
        {
            Endpoint = ValidEndpoint,
            ApiKey = ValidApiKey
        };

        // Act
        var result = await _controller.DiscoverAzureModels(request, CancellationToken.None);

        // Assert — 400, not 502
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);

        // Security: the raw exception message (which could reference credentials) must
        // not appear in the response body. Verify via the serialised anonymous object.
        var body = badRequest.Value!.ToString()!;
        Assert.DoesNotContain(ValidApiKey, body);
        Assert.Contains("error", body, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Scenario 3a: Missing endpoint field — 400 Bad Request
    // FR: "if either is absent the request is rejected with 400 Bad Request"
    //     "indicating which field is missing"
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DiscoverAzureModels_WhenEndpointIsNull_Returns400NamingEndpoint()
    {
        // Arrange
        var request = new DiscoverAzureModelsRequest
        {
            Endpoint = null,
            ApiKey = ValidApiKey
        };

        // Act
        var result = await _controller.DiscoverAzureModels(request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var body = badRequest.Value!.ToString()!;
        Assert.Contains("endpoint", body, StringComparison.OrdinalIgnoreCase);

        // Discovery service must NOT be called when validation fails
        await _mockDiscoveryService
            .DidNotReceive()
            .DiscoverModelsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverAzureModels_WhenEndpointIsWhitespace_Returns400NamingEndpoint()
    {
        // Arrange
        var request = new DiscoverAzureModelsRequest
        {
            Endpoint = "   ",
            ApiKey = ValidApiKey
        };

        // Act
        var result = await _controller.DiscoverAzureModels(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        await _mockDiscoveryService
            .DidNotReceive()
            .DiscoverModelsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Scenario 3b: Missing apiKey field — 400 Bad Request
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DiscoverAzureModels_WhenApiKeyIsNull_Returns400NamingApiKey()
    {
        // Arrange
        var request = new DiscoverAzureModelsRequest
        {
            Endpoint = ValidEndpoint,
            ApiKey = null
        };

        // Act
        var result = await _controller.DiscoverAzureModels(request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var body = badRequest.Value!.ToString()!;
        Assert.Contains("apiKey", body, StringComparison.OrdinalIgnoreCase);

        await _mockDiscoveryService
            .DidNotReceive()
            .DiscoverModelsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscoverAzureModels_WhenApiKeyIsWhitespace_Returns400NamingApiKey()
    {
        // Arrange
        var request = new DiscoverAzureModelsRequest
        {
            Endpoint = ValidEndpoint,
            ApiKey = "   "
        };

        // Act
        var result = await _controller.DiscoverAzureModels(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        await _mockDiscoveryService
            .DidNotReceive()
            .DiscoverModelsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
