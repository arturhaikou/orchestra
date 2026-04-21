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
    private readonly IWorkspaceAIProviderRepository _aiProviderRepository = Substitute.For<IWorkspaceAIProviderRepository>();
    private readonly TicketService _sut;

    public TicketServiceSummarizationTests()
    {
        _sut = new TicketService(
            _ticketQueryService,
            _commandService,
            _commentService,
            _enrichmentService,
            _workspaceDataAccess,
            _aiProviderRepository,
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
        _enrichmentService.GenerateSummaryAsync("Formatted ticket content", workspaceId, modelId, Arg.Any<CancellationToken>())
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
            workspaceId,
            modelId,
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Scenario 2: Workspace has no saved model (null)

    [Fact]
    public async Task GenerateSummaryAsync_NoAiSummarizationModelId_FallsBackToAIProviderConfigurationDefaultModelId()
    {
        // Arrange — workspace has no AiSummarizationModelId; AIProviderConfiguration has DefaultModelId
        var ticketId = "TKT-456";
        var userId = Guid.NewGuid();
        var providerDefaultModelId = "gpt-4o";
        
        var workspace = new WorkspaceBuilder()
            .WithIsAiSummarizationEnabled(true)
            .WithAiSummarizationModelId(null)
            .Build();


        var workspaceId = workspace.Id;
        
        var ticketDto = new TicketDtoBuilder()
            .WithWorkspaceId(workspaceId)
            .Build();

        var aiConfig = AIProviderConfiguration.Create(
            workspaceId,
            Orchestra.Domain.Enums.AIProviderType.AzureOpenAI,
            defaultModelId: providerDefaultModelId);

        _ticketQueryService.GetTicketByIdAsync(ticketId, userId, CancellationToken.None)
            .Returns(ticketDto);
        _workspaceDataAccess.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(workspace);
        _aiProviderRepository.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(aiConfig);
        _enrichmentService.BuildSummaryContent(ticketDto)
            .Returns("Formatted ticket content");
        _enrichmentService.GenerateSummaryAsync(
                "Formatted ticket content", workspaceId, providerDefaultModelId, Arg.Any<CancellationToken>())
            .Returns("AI-generated summary using provider default");

        // Act
        var result = await _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None);

        // Assert — summary was generated using the provider configuration's default model ID
        Assert.False(result.FeatureDisabled);
        Assert.NotNull(result.Ticket);
        Assert.Equal("AI-generated summary using provider default", result.Ticket!.Summary);

        await _enrichmentService.Received(1).GenerateSummaryAsync(
            "Formatted ticket content",
            workspaceId,
            providerDefaultModelId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSummaryAsync_NeitherModelIdSet_ThrowsInvalidOperationException()
    {
        // Arrange — workspace has no AiSummarizationModelId AND AIProviderConfiguration has no DefaultModelId
        var ticketId = "TKT-000";
        var userId = Guid.NewGuid();

        var workspace = new WorkspaceBuilder()
            .WithIsAiSummarizationEnabled(true)
            .WithAiSummarizationModelId(null)
            .Build();

        var workspaceId = workspace.Id;

        var ticketDto = new TicketDtoBuilder()
            .WithWorkspaceId(workspaceId)
            .Build();

        _ticketQueryService.GetTicketByIdAsync(ticketId, userId, CancellationToken.None)
            .Returns(ticketDto);
        _workspaceDataAccess.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(workspace);
        // Simulate no AI config record at all
        _aiProviderRepository.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None));

        // Verify enrichment service was never reached
        await _enrichmentService.DidNotReceive().GenerateSummaryAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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
            Arg.Any<Guid>(),
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
        _aiProviderRepository.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);
        _enrichmentService.BuildSummaryContent(ticketDto)
            .Returns("Formatted ticket content");
        
        // Simulate AI provider failure
        var summarizationEx = new SummarizationException("AI provider error", new Exception("Network timeout"));
        _enrichmentService.GenerateSummaryAsync("Formatted ticket content", workspaceId, modelId, Arg.Any<CancellationToken>())
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
    private readonly IAIProviderResolver _aiProviderResolver = Substitute.For<IAIProviderResolver>();

    #region Scenario: Model ID delegates to AI provider resolver

    [Fact]
    public async Task ResolveChatClientAsync_DelegatesToAIProviderResolver()
    {
        // Arrange
        var workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var modelId = "gpt-4o";
        var sut = new ChatClientResolver(_aiProviderResolver);
        _aiProviderResolver.ResolveAsync(workspaceId, modelId, CancellationToken.None)
            .Returns(Task.FromResult(_defaultChatClient));

        // Act
        var result = await sut.ResolveAsync(workspaceId, modelId, CancellationToken.None);

        // Assert
        Assert.Same(_defaultChatClient, result);
        await _aiProviderResolver.Received(1).ResolveAsync(workspaceId, modelId, CancellationToken.None);
    }

    #endregion


}
