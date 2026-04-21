using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestra.ApiService.Controllers;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Domain.Enums;
using NSubstitute.ExceptionExtensions;

namespace Orchestra.ApiService.Tests.Tests.Workspaces;

public class WorkspaceControllerProviderReconfigurationTests
{
    private readonly IWorkspaceService _mockWorkspaceService;
    private readonly IWorkspaceProviderService _mockProviderService;
    private readonly IWorkspaceAuthorizationService _mockAuthService;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public WorkspaceControllerProviderReconfigurationTests()
    {
        _mockWorkspaceService = Substitute.For<IWorkspaceService>();
        _mockProviderService  = Substitute.For<IWorkspaceProviderService>();
        _mockAuthService      = Substitute.For<IWorkspaceAuthorizationService>();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private WorkspaceController CreateController(Guid? userId = null)
    {
        var controller = new WorkspaceController(
            _mockWorkspaceService,
            _mockProviderService,
            _mockAuthService,
            NullLogger<WorkspaceController>.Instance);

        var id = userId ?? _userId;
        var claimsIdentity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString())
        });

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(claimsIdentity)
            }
        };

        return controller;
    }

    private WorkspaceController CreateControllerWithNoUserClaim()
    {
        var controller = new WorkspaceController(
            _mockWorkspaceService,
            _mockProviderService,
            _mockAuthService,
            NullLogger<WorkspaceController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()) // no claims
            }
        };

        return controller;
    }

    private static ReconfigureProviderRequest ValidAzureRequest(string defaultModelId = "gpt-4o") =>
        new ReconfigureProviderRequest(
            ProviderType: "AzureOpenAI",
            Endpoint: "https://my-resource.openai.azure.com/",
            ApiKey: "test-key-123",
            DefaultModelId: defaultModelId);

    private static ReconfigureProviderRequest ValidOllamaRequest(string defaultModelId = "llama3.2") =>
        new ReconfigureProviderRequest(
            ProviderType: "Ollama",
            Endpoint: "http://localhost:11434",
            ApiKey: null,
            DefaultModelId: defaultModelId);

    // ===============================================================
    // Scenario 1: Owner reconfigures with valid credentials → 204
    // ===============================================================

    [Fact]
    public async Task ReconfigureProvider_WhenOwnerWithValidOllamaCredentials_Returns204NoContent()
    {
        // Arrange
        _mockAuthService
            .EnsureUserIsOwnerAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _mockProviderService
            .ReconfigureProviderAsync(
                _workspaceId,
                AIProviderType.Ollama,
                "http://localhost:11434",
                null,
                "llama3.2",
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = CreateController();

        // Act
        var result = await controller.ReconfigureProvider(
            _workspaceId, ValidOllamaRequest(), CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    // ===============================================================
    // Scenario 2: Invalid credentials prevent update → 422
    // ===============================================================

    [Fact]
    public async Task ReconfigureProvider_WhenCredentialProbeFails_Returns422UnprocessableEntity()
    {
        // Arrange
        _mockAuthService
            .EnsureUserIsOwnerAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _mockProviderService
            .ReconfigureProviderAsync(
                _workspaceId,
                AIProviderType.AzureOpenAI,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderReconfigurationException(
                "Failed to communicate with the Azure OpenAI provider."));

        var controller = CreateController();

        // Act
        var result = await controller.ReconfigureProvider(
            _workspaceId, ValidAzureRequest(), CancellationToken.None);

        // Assert
        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponse>(unprocessable.Value);
        Assert.Contains("Azure OpenAI", errorResponse.Message);
    }

    // ===============================================================
    // Scenario 3: defaultModelId not in validated model list → 422
    // ===============================================================

    [Fact]
    public async Task ReconfigureProvider_WhenDefaultModelIdNotInList_Returns422UnprocessableEntity()
    {
        // Arrange
        _mockAuthService
            .EnsureUserIsOwnerAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _mockProviderService
            .ReconfigureProviderAsync(
                _workspaceId,
                AIProviderType.AzureOpenAI,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                "nonexistent-model",
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderReconfigurationException(
                "The model 'nonexistent-model' is not available from the configured provider."));

        var controller = CreateController();
        var request = ValidAzureRequest(defaultModelId: "nonexistent-model");

        // Act
        var result = await controller.ReconfigureProvider(
            _workspaceId, request, CancellationToken.None);

        // Assert
        Assert.IsType<UnprocessableEntityObjectResult>(result);
    }

    // ===============================================================
    // Scenario 4: Non-owner member is forbidden → 403
    // ===============================================================

    [Fact]
    public async Task ReconfigureProvider_WhenMemberButNotOwner_Returns403Forbidden()
    {
        // Arrange
        _mockAuthService
            .EnsureUserIsOwnerAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new WorkspaceForbiddenException(_userId, _workspaceId));

        var controller = CreateController();

        // Act
        var result = await controller.ReconfigureProvider(
            _workspaceId, ValidAzureRequest(), CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);

        // Service must NOT be called after authorization failure
        await _mockProviderService
            .DidNotReceive()
            .ReconfigureProviderAsync(
                Arg.Any<Guid>(), Arg.Any<AIProviderType>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    // ===============================================================
    // Scenario 5: Non-member is denied → 404
    // ===============================================================

    [Fact]
    public async Task ReconfigureProvider_WhenNonMember_Returns404NotFound()
    {
        // Arrange
        _mockAuthService
            .EnsureUserIsOwnerAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(_userId, _workspaceId));

        var controller = CreateController();

        // Act
        var result = await controller.ReconfigureProvider(
            _workspaceId, ValidAzureRequest(), CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    // ===============================================================
    // Scenario 6: Unauthenticated request is rejected → 401
    // ===============================================================

    [Fact]
    public async Task ReconfigureProvider_WhenNoJwtClaim_Returns401Unauthorized()
    {
        // Arrange
        var controller = CreateControllerWithNoUserClaim();

        // Act
        var result = await controller.ReconfigureProvider(
            _workspaceId, ValidAzureRequest(), CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    // ===============================================================
    // Body validation — missing providerType → 400
    // ===============================================================

    [Fact]
    public async Task ReconfigureProvider_WhenProviderTypeIsMissing_Returns400BadRequest()
    {
        // Arrange
        var controller = CreateController();
        var request = new ReconfigureProviderRequest(
            ProviderType: null,
            Endpoint: "https://my-resource.openai.azure.com/",
            ApiKey: "test-key",
            DefaultModelId: "gpt-4o");

        // Act
        var result = await controller.ReconfigureProvider(
            _workspaceId, request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ===============================================================
    // Body validation — AzureOpenAI with ollamaBaseUrl → 400
    // ===============================================================

    [Fact]
    public async Task ReconfigureProvider_WhenOllamaWithApiKey_Returns400BadRequest()
    {
        // Arrange
        var controller = CreateController();
        var inconsistentRequest = new ReconfigureProviderRequest(
            ProviderType: "Ollama",
            Endpoint: "http://localhost:11434",
            ApiKey: "should-not-be-here",   // apiKey must be absent for Ollama
            DefaultModelId: "llama3.2");

        // Act
        var result = await controller.ReconfigureProvider(
            _workspaceId, inconsistentRequest, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ===============================================================
    // Body validation — missing defaultModelId → 400
    // ===============================================================

    [Fact]
    public async Task ReconfigureProvider_WhenDefaultModelIdIsMissing_Returns400BadRequest()
    {
        // Arrange
        var controller = CreateController();
        var request = new ReconfigureProviderRequest(
            ProviderType: "AzureOpenAI",
            Endpoint: "https://my-resource.openai.azure.com/",
            ApiKey: "test-key",
            DefaultModelId: null);  // missing

        // Act
        var result = await controller.ReconfigureProvider(
            _workspaceId, request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
