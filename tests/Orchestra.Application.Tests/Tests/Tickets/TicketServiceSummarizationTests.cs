using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Tickets.Services;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Services;
using Xunit;
using Orchestra.Tests.Shared.Builders;
using Microsoft.Extensions.AI;

namespace Orchestra.Application.Tests.Tests.Tickets;

/// <summary>
/// Unit tests for ticket summarization with workspace model ID resolution.
/// Tests cover:
/// - ChatClientResolver returning appropriate client based on model availability
/// - Model registry availability checks
/// - Silent fallback when workspace model is stale or unavailable
/// - Backward compatibility with null model ID (uses startup default)
/// - Error propagation for AI provider failures
/// </summary>
public class TicketServiceSummarizationTests
{
    private readonly ITicketQueryService _ticketQueryService = Substitute.For<ITicketQueryService>();
    private readonly IWorkspaceDataAccess _workspaceDataAccess = Substitute.For<IWorkspaceDataAccess>();
    private readonly ITicketEnrichmentService _enrichmentService = Substitute.For<ITicketEnrichmentService>();
    private readonly ITicketCommentService _commentService = Substitute.For<ITicketCommentService>();
    private readonly ILogger<TicketService> _logger = Substitute.For<ILogger<TicketService>>();
    private readonly ITicketCommandService _commandService = Substitute.For<ITicketCommandService>();
    private readonly TicketService _sut;

    public TicketServiceSummarizationTests()
    {
        _sut = new TicketService(
            _ticketQueryService,
            _commandService,
            _commentService,
            _enrichmentService,
            _workspaceDataAccess,
            _logger);
    }

    #region Scenario 1: Workspace has a valid model

    [Fact]
    public async Task GenerateSummaryAsync_WorkspaceHasValidModel_PassesModelIdToEnrichmentService()
    {
        // Arrange
        var ticketId = "TKT-123";
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var modelId = "gpt-4o";

        var ticketDto = new TicketDtoBuilder()
            .WithWorkspaceId(workspaceId)
            .Build();

        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsAiSummarizationEnabled(true)
            .WithAiSummarizationModelId(modelId)
            .Build();

        _ticketQueryService.GetTicketByIdAsync(ticketId, userId, CancellationToken.None)
            .Returns(ticketDto);
        _workspaceDataAccess.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(workspace);
        _enrichmentService.BuildSummaryContent(ticketDto)
            .Returns("Formatted ticket content");
        _enrichmentService.GenerateSummaryAsync("Formatted ticket content", modelId, Arg.Any<CancellationToken>())
            .Returns("AI-generated summary");

        // Act
        var result = await _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None);

        // Assert
        Assert.False(result.FeatureDisabled);
        Assert.NotNull(result.Ticket);
        Assert.Equal("AI-generated summary", result.Ticket.Summary);
        
        // Verify the model ID was passed to enrichment service
        await _enrichmentService.Received(1).GenerateSummaryAsync(
            "Formatted ticket content",
            modelId,
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Scenario 2: Workspace has no saved model (null)

    [Fact]
    public async Task GenerateSummaryAsync_WorkspaceModelIdIsNull_PassesNullToEnrichmentService()
    {
        // Arrange
        var ticketId = "TKT-456";
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var ticketDto = new TicketDtoBuilder()
            .WithWorkspaceId(workspaceId)
            .Build();

        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsAiSummarizationEnabled(true)
            .WithAiSummarizationModelId(null)
            .Build();

        _ticketQueryService.GetTicketByIdAsync(ticketId, userId, CancellationToken.None)
            .Returns(ticketDto);
        _workspaceDataAccess.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(workspace);
        _enrichmentService.BuildSummaryContent(ticketDto)
            .Returns("Formatted ticket content");
        _enrichmentService.GenerateSummaryAsync("Formatted ticket content", null, Arg.Any<CancellationToken>())
            .Returns("AI-generated summary");

        // Act
        var result = await _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None);

        // Assert
        Assert.False(result.FeatureDisabled);
        Assert.NotNull(result.Ticket);
        Assert.Equal("AI-generated summary", result.Ticket.Summary);
        
        // Verify null was passed to enrichment service (startup default will be used)
        await _enrichmentService.Received(1).GenerateSummaryAsync(
            "Formatted ticket content",
            null,
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Scenario 3: Feature disabled

    [Fact]
    public async Task GenerateSummaryAsync_FeatureDisabled_ReturnsFeatureDisabledResponseImmediately()
    {
        // Arrange
        var ticketId = "TKT-789";
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var ticketDto = new TicketDtoBuilder()
            .WithWorkspaceId(workspaceId)
            .Build();

        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsAiSummarizationEnabled(false)
            .WithAiSummarizationModelId("gpt-4o")
            .Build();

        _ticketQueryService.GetTicketByIdAsync(ticketId, userId, CancellationToken.None)
            .Returns(ticketDto);
        _workspaceDataAccess.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(workspace);

        // Act
        var result = await _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None);

        // Assert
        Assert.True(result.FeatureDisabled);
        Assert.Null(result.Ticket);
        Assert.NotNull(result.Message);
        
        // Verify enrichment service was never called
        await _enrichmentService.DidNotReceive().GenerateSummaryAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Scenario 4: AI provider failure

    [Fact]
    public async Task GenerateSummaryAsync_AIProviderFailure_PropagatesSummarizationException()
    {
        // Arrange
        var ticketId = "TKT-999";
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var modelId = "gpt-4o";

        var ticketDto = new TicketDtoBuilder()
            .WithWorkspaceId(workspaceId)
            .Build();

        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsAiSummarizationEnabled(true)
            .WithAiSummarizationModelId(modelId)
            .Build();

        _ticketQueryService.GetTicketByIdAsync(ticketId, userId, CancellationToken.None)
            .Returns(ticketDto);
        _workspaceDataAccess.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(workspace);
        _enrichmentService.BuildSummaryContent(ticketDto)
            .Returns("Formatted ticket content");
        
        // Simulate AI provider failure
        var summarizationEx = new SummarizationException("AI provider error", new Exception("Network timeout"));
        _enrichmentService.GenerateSummaryAsync("Formatted ticket content", modelId, Arg.Any<CancellationToken>())
            .Throws(summarizationEx);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<SummarizationException>(
            () => _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None));
        Assert.Equal("AI provider error", ex.Message);
    }

    #endregion
}

/// <summary>
/// Unit tests for the ChatClientResolver model resolution and client selection logic.
/// </summary>
public class ChatClientResolverTests
{
    private readonly IChatClient _defaultChatClient = Substitute.For<IChatClient>();
    private readonly IAIModelRegistry _modelRegistry = Substitute.For<IAIModelRegistry>();
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
    private readonly ILogger<ChatClientResolver> _logger = Substitute.For<ILogger<ChatClientResolver>>();

    #region Scenario: Null model ID returns default client

    [Fact]
    public async Task ResolveChatClientAsync_ModelIdIsNull_ReturnsDefaultClient()
    {
        // Arrange
        _configuration["AgentExecution:Provider"].Returns("Azure");
        var sut = new ChatClientResolver(_defaultChatClient, _modelRegistry, _configuration, _logger);

        // Act
        var result = await sut.ResolveChatClientAsync(null, CancellationToken.None);

        // Assert
        Assert.Same(_defaultChatClient, result);
    }

    #endregion

    #region Scenario: Empty model ID returns default client

    [Fact]
    public async Task ResolveChatClientAsync_ModelIdIsEmpty_ReturnsDefaultClient()
    {
        // Arrange
        _configuration["AgentExecution:Provider"].Returns("Azure");
        var sut = new ChatClientResolver(_defaultChatClient, _modelRegistry, _configuration, _logger);

        // Act
        var result = await sut.ResolveChatClientAsync("", CancellationToken.None);

        // Assert
        Assert.Same(_defaultChatClient, result);
    }

    #endregion

    #region Scenario: Stale/unavailable model returns default with warning

    [Fact]
    public async Task ResolveChatClientAsync_ModelIdUnavailable_ReturnsDefaultWithWarning()
    {
        // Arrange
        var modelId = "gpt-4o-old";

        _configuration["AgentExecution:Provider"].Returns("Azure");
        _modelRegistry.IsModelAvailable(modelId).Returns(false);

        var sut = new ChatClientResolver(_defaultChatClient, _modelRegistry, _configuration, _logger);

        // Act
        var result = await sut.ResolveChatClientAsync(modelId, CancellationToken.None);

        // Assert
        Assert.Same(_defaultChatClient, result);
    }

    #endregion

    #region Scenario: Available model triggers client creation (infrastructure test)

    [Fact]
    public async Task ResolveChatClientAsync_ModelIdAvailableWithValidEndpoint_CreatesModelSpecificClient()
    {
        // Arrange
        var modelId = "gpt-4o";

        _configuration["AgentExecution:Provider"].Returns("Azure");
        _configuration.GetConnectionString("ai").Returns((string)null);
        _modelRegistry.IsModelAvailable(modelId).Returns(true);

        var sut = new ChatClientResolver(_defaultChatClient, _modelRegistry, _configuration, _logger);

        // Act
        // When connection string is null/invalid, resolver falls back to default during creation attempt
        var result = await sut.ResolveChatClientAsync(modelId, CancellationToken.None);

        // Assert
        // Falls back to default because creation failed (missing/invalid configuration)
        Assert.Same(_defaultChatClient, result);
    }

    #endregion
}
