using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Tickets.Services;
using Orchestra.Domain.Entities;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tickets;

public class TicketServiceTests
{
    private readonly ITicketQueryService _queryServiceMock = Substitute.For<ITicketQueryService>();
    private readonly ITicketCommandService _commandServiceMock = Substitute.For<ITicketCommandService>();
    private readonly ITicketCommentService _commentServiceMock = Substitute.For<ITicketCommentService>();
    private readonly ITicketEnrichmentService _enrichmentServiceMock = Substitute.For<ITicketEnrichmentService>();
    private readonly IWorkspaceDataAccess _workspaceDataAccessMock = Substitute.For<IWorkspaceDataAccess>();
    private readonly ILogger<TicketService> _loggerMock = Substitute.For<ILogger<TicketService>>();
    private readonly TicketService _sut;

    public TicketServiceTests()
    {
        _sut = new TicketService(
            _queryServiceMock,
            _commandServiceMock,
            _commentServiceMock,
            _enrichmentServiceMock,
            _workspaceDataAccessMock,
            _loggerMock);
    }

    [Fact]
    public async Task GenerateSummaryAsync_FeatureDisabled_ReturnsFeatureDisabledResponse()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid().ToString();

        var ticket = new TicketDtoBuilder()
            .WithId(ticketId)
            .WithWorkspaceId(workspaceId)
            .WithTitle("Test Ticket")
            .Build();

        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsAiSummarizationEnabled(false)
            .Build();

        _queryServiceMock.GetTicketByIdAsync(ticketId, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ticket));

        _workspaceDataAccessMock.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));

        // Act
        var response = await _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None);

        // Assert
        Assert.True(response.FeatureDisabled);
        Assert.Null(response.Ticket);
        Assert.Equal("Summarization is not enabled for this workspace. Go to workspace settings to enable it.", response.Message);
        
        // Verify AI enrichment service was never called
        await _enrichmentServiceMock.DidNotReceive().GenerateSummaryAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSummaryAsync_FeatureEnabled_CallsEnrichmentAndReturnsSummary()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid().ToString();
        var generatedSummary = "This is an AI-generated summary.";

        var ticket = new TicketDtoBuilder()
            .WithId(ticketId)
            .WithWorkspaceId(workspaceId)
            .WithTitle("Test Ticket")
            .WithDescription("Test Description")
            .WithComments(new List<CommentDto>())
            .Build();

        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsAiSummarizationEnabled(true)
            .Build();

        _queryServiceMock.GetTicketByIdAsync(ticketId, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ticket));

        _workspaceDataAccessMock.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));

        _enrichmentServiceMock.BuildSummaryContent(Arg.Any<TicketDto>())
            .Returns("content");

        _enrichmentServiceMock.GenerateSummaryAsync("content", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(generatedSummary));

        // Act
        var response = await _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None);

        // Assert
        Assert.False(response.FeatureDisabled);
        Assert.NotNull(response.Ticket);
        Assert.Null(response.Message);
        Assert.Equal(generatedSummary, response.Ticket.Summary);
        
        // Verify AI enrichment service was called
        await _enrichmentServiceMock.Received(1).GenerateSummaryAsync("content", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSummaryAsync_WorkspaceNotFound_ReturnsFeatureDisabledResponseAndLogsWarning()
    {
        // Arrange: Simulate workspace being deleted between authorization and summary generation
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid().ToString();

        var ticket = new TicketDtoBuilder()
            .WithId(ticketId)
            .WithWorkspaceId(workspaceId)
            .Build();

        _queryServiceMock.GetTicketByIdAsync(ticketId, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ticket));

        _workspaceDataAccessMock.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(null));

        // Act
        var response = await _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None);

        // Assert
        Assert.True(response.FeatureDisabled);
        Assert.Null(response.Ticket);
        Assert.Equal("Summarization is not enabled for this workspace. Go to workspace settings to enable it.", response.Message);
        
        // Verify AI enrichment service was not called
        await _enrichmentServiceMock.DidNotReceive().GenerateSummaryAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSummaryAsync_TicketNotFound_ThrowsTicketNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ticketId = "nonexistent";

        _queryServiceMock.GetTicketByIdAsync(ticketId, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TicketDto>(new TicketNotFoundException(ticketId)));

        // Act & Assert
        await Assert.ThrowsAsync<TicketNotFoundException>(
            () => _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None));
        
        // Verify workspace data access was never called (error thrown earlier)
        await _workspaceDataAccessMock.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSummaryAsync_UnauthorizedAccess_ThrowsUnauthorizedTicketAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ticketId = "unauthorized";

        _queryServiceMock.GetTicketByIdAsync(ticketId, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TicketDto>(new UnauthorizedTicketAccessException(userId, ticketId)));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedTicketAccessException>(
            () => _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None));
        
        // Verify workspace data access was never called (error thrown earlier)
        await _workspaceDataAccessMock.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSummaryAsync_AI_ServiceFailure_PropagatesSummarizationException()
    {
        // Arrange: Feature is enabled, but AI service fails
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid().ToString();

        var ticket = new TicketDtoBuilder()
            .WithId(ticketId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsAiSummarizationEnabled(true)
            .Build();

        _queryServiceMock.GetTicketByIdAsync(ticketId, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ticket));

        _workspaceDataAccessMock.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));

        _enrichmentServiceMock.BuildSummaryContent(Arg.Any<TicketDto>())
            .Returns("content");

        _enrichmentServiceMock.GenerateSummaryAsync("content", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new SummarizationException("AI service unavailable")));

        // Act & Assert
        await Assert.ThrowsAsync<SummarizationException>(
            () => _sut.GenerateSummaryAsync(ticketId, userId, CancellationToken.None));
    }
}
