using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Orchestra.ApiService.Controllers;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workspaces.DTOs;

namespace Orchestra.ApiService.Tests.Tests.Provider;

public class ProviderControllerOllamaDiscoveryTests
{
    private readonly IOllamaModelDiscoveryService _mockOllamaDiscoveryService;
    private readonly ProviderController _controller;

    private const string ValidEndpoint = "http://localhost:11434";

    public ProviderControllerOllamaDiscoveryTests()
    {
        _mockOllamaDiscoveryService = Substitute.For<IOllamaModelDiscoveryService>();
        var stubAzureDiscoveryService = Substitute.For<IAzureOpenAIModelDiscoveryService>();
        _controller = new ProviderController(stubAzureDiscoveryService, _mockOllamaDiscoveryService);
    }

    // -----------------------------------------------------------------------
    // Scenario 1: Reachable Ollama server with models available
    // FR: isValid: true, non-empty models array, errorMessage: null
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DiscoverOllamaModels_WhenServerReachableWithModels_Returns200WithValidResult()
    {
        // Arrange
        var expectedModels = new List<string> { "llama3:latest", "mistral:7b" }.AsReadOnly();
        var serviceResult = new OllamaDiscoveryResult(
            IsValid: true,
            Models: expectedModels,
            ErrorMessage: null);

        _mockOllamaDiscoveryService
            .DiscoverModelsAsync(ValidEndpoint, Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        var request = new DiscoverOllamaModelsRequest { Endpoint = ValidEndpoint };

        // Act
        var result = await _controller.DiscoverOllamaModels(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OllamaDiscoveryResult>(okResult.Value);
        Assert.True(response.IsValid);
        Assert.Equal(expectedModels, response.Models);
        Assert.Null(response.ErrorMessage);
    }

    // -----------------------------------------------------------------------
    // Scenario 2: Reachable Ollama server with no models installed
    // FR: isValid: true, empty models array, errorMessage: null
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DiscoverOllamaModels_WhenServerReachableWithNoModels_Returns200WithEmptyList()
    {
        // Arrange
        var serviceResult = new OllamaDiscoveryResult(
            IsValid: true,
            Models: Array.Empty<string>(),
            ErrorMessage: null);

        _mockOllamaDiscoveryService
            .DiscoverModelsAsync(ValidEndpoint, Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        var request = new DiscoverOllamaModelsRequest { Endpoint = ValidEndpoint };

        // Act
        var result = await _controller.DiscoverOllamaModels(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OllamaDiscoveryResult>(okResult.Value);
        Assert.True(response.IsValid);
        Assert.Empty(response.Models);
        Assert.Null(response.ErrorMessage);
    }

    // -----------------------------------------------------------------------
    // Scenario 3: Ollama server unreachable or returns error
    // FR: isValid: false, empty models array, non-null errorMessage
    //     errorMessage must NOT echo the submitted endpoint URL
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DiscoverOllamaModels_WhenServerUnreachable_Returns200WithInvalidResult()
    {
        // Arrange
        var serviceResult = new OllamaDiscoveryResult(
            IsValid: false,
            Models: Array.Empty<string>(),
            ErrorMessage: "The Ollama server could not be reached.");

        _mockOllamaDiscoveryService
            .DiscoverModelsAsync(ValidEndpoint, Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        var request = new DiscoverOllamaModelsRequest { Endpoint = ValidEndpoint };

        // Act
        var result = await _controller.DiscoverOllamaModels(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OllamaDiscoveryResult>(okResult.Value);
        Assert.False(response.IsValid);
        Assert.Empty(response.Models);
        Assert.NotNull(response.ErrorMessage);

        // Security: the submitted endpoint URL must never appear in the error message.
        Assert.DoesNotContain(ValidEndpoint, response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Scenario 4a: Missing endpoint field — 400 Bad Request naming "endpoint"
    // FR: "400 Bad Request is returned naming endpoint as the missing field"
    //     "no Ollama call is made"
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DiscoverOllamaModels_WhenEndpointIsNull_Returns400NamingEndpoint()
    {
        // Arrange
        var request = new DiscoverOllamaModelsRequest { Endpoint = null };

        // Act
        var result = await _controller.DiscoverOllamaModels(request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var body = badRequest.Value!.ToString()!;
        Assert.Contains("endpoint", body, StringComparison.OrdinalIgnoreCase);

        // Discovery service must NOT be called when validation fails
        await _mockOllamaDiscoveryService
            .DidNotReceive()
            .DiscoverModelsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Scenario 4b: Blank endpoint field — 400 Bad Request
    // FR: whitespace-only value is treated as missing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DiscoverOllamaModels_WhenEndpointIsWhitespace_Returns400NamingEndpoint()
    {
        // Arrange
        var request = new DiscoverOllamaModelsRequest { Endpoint = "   " };

        // Act
        var result = await _controller.DiscoverOllamaModels(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);

        await _mockOllamaDiscoveryService
            .DidNotReceive()
            .DiscoverModelsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
