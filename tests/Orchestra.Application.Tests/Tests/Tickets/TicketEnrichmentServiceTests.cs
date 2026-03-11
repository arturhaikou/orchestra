using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Tickets.Services;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tickets
{
    public class TicketEnrichmentServiceTests
    {
        private readonly ISentimentAnalysisService _sentimentMock = Substitute.For<ISentimentAnalysisService>();
        private readonly ISummarizationService _summarizationMock = Substitute.For<ISummarizationService>();
        private readonly ILogger<TicketEnrichmentService> _loggerMock = Substitute.For<ILogger<TicketEnrichmentService>>();
        private readonly TicketEnrichmentService _sut;

        public TicketEnrichmentServiceTests()
        {
            _sut = new TicketEnrichmentService(_sentimentMock, _summarizationMock, _loggerMock);
        }

        [Fact]
        public async Task CalculateSentimentAsync_InternalTicket_SetsSatisfaction100()
        {

            var ticket = new TicketDtoBuilder()
                .AsInternal(true)
                .WithComments(new List<CommentDto>())
                .Build();
            var tickets = new List<TicketDto> { ticket };

            await _sut.CalculateSentimentAsync(tickets, null, CancellationToken.None);

            Assert.Equal(100, tickets[0].Satisfaction);
        }

        [Fact]
        public async Task CalculateSentimentAsync_ExternalTicketWithNoComments_SetsSatisfaction100()
        {

            var ticket = new TicketDtoBuilder()
                .AsExternal(Guid.NewGuid(), "EXT-1")
                .WithComments(new List<CommentDto>())
                .Build();
            var tickets = new List<TicketDto> { ticket };

            await _sut.CalculateSentimentAsync(tickets, null, CancellationToken.None);

            Assert.Equal(100, tickets[0].Satisfaction);
        }

        [Fact]
        public async Task CalculateSentimentAsync_ExternalTicketWithComments_UsesSentimentService()
        {

            var ticket = new TicketDtoBuilder()
                .WithId("T1")
                .AsExternal(Guid.NewGuid(), "EXT-1")
                .WithComments(new List<CommentDto> { new("C1", "A1", "Good job!") })
                .Build();
            var tickets = new List<TicketDto> { ticket };

            _sentimentMock.AnalyzeBatchSentimentAsync(
                Arg.Is<List<TicketSentimentRequest>>(reqs => reqs.Count == 1 && reqs[0].WorkspaceId == ticket.WorkspaceId && reqs[0].TicketId == ticket.Id), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<TicketSentimentResult> { new("T1", 85) }));

            await _sut.CalculateSentimentAsync(tickets, null, CancellationToken.None);

            Assert.Equal(85, tickets[0].Satisfaction);
        }

        [Fact]
        public async Task CalculateSentimentAsync_SentimentServiceThrows_SetsSatisfaction100()
        {

            var ticket = new TicketDtoBuilder()
                .WithId("T2")
                .AsExternal(Guid.NewGuid(), "EXT-2")
                .WithComments(new List<CommentDto> { new("C2", "A2", "Needs work.") })
                .Build();
            var tickets = new List<TicketDto> { ticket };

            _sentimentMock.AnalyzeBatchSentimentAsync(
                Arg.Is<List<TicketSentimentRequest>>(reqs => reqs.Count == 1 && reqs[0].WorkspaceId == ticket.WorkspaceId && reqs[0].TicketId == ticket.Id), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<List<TicketSentimentResult>>(new Exception("fail")));

            await _sut.CalculateSentimentAsync(tickets, null, CancellationToken.None);

            Assert.Equal(100, tickets[0].Satisfaction);
        }

        [Fact]
        public async Task CalculateSentimentForSingleAsync_InternalTicket_ReturnsSatisfaction100()
        {

            var ticket = new TicketDtoBuilder()
                .WithId("T3")
                .AsInternal(true)
                .WithComments(new List<CommentDto>())
                .Build();

            var result = await _sut.CalculateSentimentForSingleAsync(ticket, null, CancellationToken.None);

            Assert.Equal(100, result.Satisfaction);
        }

        [Fact]
        public async Task CalculateSentimentForSingleAsync_ExternalTicketWithComments_UsesSentimentService()
        {

            var ticket = new TicketDtoBuilder()
                .WithId("T4")
                .AsExternal(Guid.NewGuid(), "EXT-4")
                .WithComments(new List<CommentDto> { new("C4", "A4", "Excellent!") })
                .Build();

            _sentimentMock.AnalyzeBatchSentimentAsync(
                Arg.Is<List<TicketSentimentRequest>>(reqs => reqs.Count == 1 && reqs[0].WorkspaceId == ticket.WorkspaceId && reqs[0].TicketId == ticket.Id), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<TicketSentimentResult> { new("T4", 90) }));

            var result = await _sut.CalculateSentimentForSingleAsync(ticket, null, CancellationToken.None);

            Assert.Equal(90, result.Satisfaction);
        }

        [Fact]
        public async Task CalculateSentimentForSingleAsync_SentimentServiceThrows_ReturnsSatisfaction100()
        {

            var ticket = new TicketDtoBuilder()
                .WithId("T5")
                .AsExternal(Guid.NewGuid(), "EXT-5")
                .WithComments(new List<CommentDto> { new("C5", "A5", "Bad experience.") })
                .Build();

            _sentimentMock.AnalyzeBatchSentimentAsync(
                Arg.Is<List<TicketSentimentRequest>>(reqs => reqs.Count == 1 && reqs[0].WorkspaceId == ticket.WorkspaceId && reqs[0].TicketId == ticket.Id), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<List<TicketSentimentResult>>(new Exception("fail")));

            var result = await _sut.CalculateSentimentForSingleAsync(ticket, null, CancellationToken.None);

            Assert.Equal(100, result.Satisfaction);
        }

        [Fact]
        public async Task CalculateSentimentAsync_WithValidModelId_PassesModelIdToSentimentService()
        {
            // Arrange
            const string workspaceModelId = "gpt-4-turbo";
            var ticket = new TicketDtoBuilder()
                .WithId("T-Valid-1")
                .AsExternal(Guid.NewGuid(), "EXT-1")
                .WithComments(new List<CommentDto> { new("C1", "A1", "Good service!") })
                .Build();
            var tickets = new List<TicketDto> { ticket };

            _sentimentMock.AnalyzeBatchSentimentAsync(
                Arg.Any<List<TicketSentimentRequest>>(), 
                Arg.Is(workspaceModelId), 
                Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<TicketSentimentResult> { new("T-Valid-1", 85) }));

            // Act
            await _sut.CalculateSentimentAsync(tickets, workspaceModelId, CancellationToken.None);

            // Assert
            await _sentimentMock.Received(1).AnalyzeBatchSentimentAsync(
                Arg.Any<List<TicketSentimentRequest>>(),
                Arg.Is(workspaceModelId),
                Arg.Any<CancellationToken>());
            Assert.Equal(85, tickets[0].Satisfaction);
        }

        [Fact]
        public async Task CalculateSentimentAsync_WithNullModelId_PassesNullToSentimentService()
        {
            // Arrange
            var ticket = new TicketDtoBuilder()
                .WithId("T-Null-1")
                .AsExternal(Guid.NewGuid(), "EXT-1")
                .WithComments(new List<CommentDto> { new("C1", "A1", "Average experience") })
                .Build();
            var tickets = new List<TicketDto> { ticket };

            _sentimentMock.AnalyzeBatchSentimentAsync(
                Arg.Any<List<TicketSentimentRequest>>(),
                Arg.Is((string?)null),
                Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<TicketSentimentResult> { new("T-Null-1", 65) }));

            // Act
            await _sut.CalculateSentimentAsync(tickets, null, CancellationToken.None);

            // Assert
            await _sentimentMock.Received(1).AnalyzeBatchSentimentAsync(
                Arg.Any<List<TicketSentimentRequest>>(),
                Arg.Is((string?)null),
                Arg.Any<CancellationToken>());
            Assert.Equal(65, tickets[0].Satisfaction);
        }

        [Fact]
        public async Task CalculateSentimentForSingleAsync_WithValidModelId_PassesModelIdToSentimentService()
        {
            // Arrange
            const string workspaceModelId = "gpt-4-turbo";
            var ticket = new TicketDtoBuilder()
                .WithId("T-Single-Valid-1")
                .AsExternal(Guid.NewGuid(), "EXT-1")
                .WithComments(new List<CommentDto> { new("C1", "A1", "Excellent support!") })
                .Build();

            _sentimentMock.AnalyzeBatchSentimentAsync(
                Arg.Any<List<TicketSentimentRequest>>(),
                Arg.Is(workspaceModelId),
                Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<TicketSentimentResult> { new("T-Single-Valid-1", 92) }));

            // Act
            var result = await _sut.CalculateSentimentForSingleAsync(ticket, workspaceModelId, CancellationToken.None);

            // Assert
            await _sentimentMock.Received(1).AnalyzeBatchSentimentAsync(
                Arg.Any<List<TicketSentimentRequest>>(),
                Arg.Is(workspaceModelId),
                Arg.Any<CancellationToken>());
            Assert.Equal(92, result.Satisfaction);
        }

        [Fact]
        public async Task CalculateSentimentForSingleAsync_WithNullModelId_DefaultsToNull()
        {
            // Arrange
            var ticket = new TicketDtoBuilder()
                .WithId("T-Single-Null-1")
                .AsExternal(Guid.NewGuid(), "EXT-1")
                .WithComments(new List<CommentDto> { new("C1", "A1", "Bad experience") })
                .Build();

            _sentimentMock.AnalyzeBatchSentimentAsync(
                Arg.Any<List<TicketSentimentRequest>>(),
                Arg.Is((string?)null),
                Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<TicketSentimentResult> { new("T-Single-Null-1", 35) }));

            // Act
            var result = await _sut.CalculateSentimentForSingleAsync(ticket, null, CancellationToken.None);

            // Assert
            await _sentimentMock.Received(1).AnalyzeBatchSentimentAsync(
                Arg.Any<List<TicketSentimentRequest>>(),
                Arg.Is((string?)null),
                Arg.Any<CancellationToken>());
            Assert.Equal(35, result.Satisfaction);
        }

        [Fact]
        public async Task GenerateSummaryAsync_DelegatesToSummarizationService()
        {

            _summarizationMock.GenerateSummaryAsync("content", Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("summary"));

            var result = await _sut.GenerateSummaryAsync("content", null, CancellationToken.None);

            Assert.Equal("summary", result);
        }

        [Fact]
        public void BuildSummaryContent_FormatsContentCorrectly()
        {

            var ticket = new TicketDtoBuilder()
                .WithId("T6")
                .AsExternal(Guid.NewGuid(), "EXT-6")
                .WithTitle("Title")
                .WithDescription("Desc")
                .WithComments(new List<CommentDto> { new("C6", "A6", "Comment 1") })
                .Build();

            var result = _sut.BuildSummaryContent(ticket);

            Assert.Contains("Title: Title", result);
            Assert.Contains("Description:", result);
            Assert.Contains("Desc", result);
            Assert.Contains("Comments:", result);
            Assert.Contains("- A6: Comment 1", result);
        }
    }
}
