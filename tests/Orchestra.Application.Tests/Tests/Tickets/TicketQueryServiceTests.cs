using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Tickets.Services;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Xunit;
public class TicketQueryServiceTests
{
    private readonly ITicketDataAccess _ticketDataAccess = Substitute.For<ITicketDataAccess>();
    private readonly IWorkspaceAuthorizationService _workspaceAuth = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly ITicketProviderFactory _ticketProviderFactory = Substitute.For<ITicketProviderFactory>();
    private readonly ITicketIdParsingService _ticketIdParsingService = Substitute.For<ITicketIdParsingService>();
    private readonly ITicketMappingService _ticketMappingService = Substitute.For<ITicketMappingService>();
    private readonly ITicketEnrichmentService _ticketEnrichmentService = Substitute.For<ITicketEnrichmentService>();
    private readonly IExternalTicketFetchingService _externalFetchingService = Substitute.For<IExternalTicketFetchingService>();
    private readonly ISentimentAnalysisService _sentimentAnalysisService = Substitute.For<ISentimentAnalysisService>();
    private readonly ITicketPaginationService _ticketPaginationService = Substitute.For<ITicketPaginationService>();
    private readonly ILogger<TicketQueryService> _logger = Substitute.For<ILogger<TicketQueryService>>();

    private TicketQueryService CreateService() => new(
        _ticketDataAccess,
        _workspaceAuth,
        _integrationDataAccess,
        _ticketProviderFactory,
        _ticketIdParsingService,
        _ticketMappingService,
        _ticketEnrichmentService,
        _externalFetchingService,
        _sentimentAnalysisService,
        _ticketPaginationService,
        _logger
    );

    private void SetupPaginationService(string phase = "internal", int offset = 0)
    {
        _ticketPaginationService.ParsePageToken(Arg.Any<string>()).Returns(new TicketPageToken { Phase = phase, InternalOffset = offset });
        _ticketPaginationService.NormalizePageSize(Arg.Any<int>()).Returns(x => (int)x[0]);
        _ticketPaginationService.SerializePageToken(Arg.Any<TicketPageToken>()).Returns("next-token");
    }

    [Fact]
    public async Task GetTicketsAsync_HandlesInvalidPageToken_Gracefully()
    {
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupPaginationService();
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 50, Arg.Any<CancellationToken>()).Returns(new List<Ticket>());
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration>());
        var service = CreateService();
        var result = await service.GetTicketsAsync(workspaceId, userId, "not-a-valid-token");
        Assert.True(result.IsLast);
    }

    [Fact]
    public async Task GetTicketByIdAsync_ThrowsTicketNotFoundException_WhenTicketMissing()
    {
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        _ticketIdParsingService.Parse(ticketId.ToString()).Returns(new TicketIdParseResult(TicketIdType.Internal, ticketId, null, null));
        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns((Ticket)null);
        var service = CreateService();
        await Assert.ThrowsAsync<TicketNotFoundException>(() => service.GetTicketByIdAsync(ticketId.ToString(), userId));
    }

    [Fact]
    public async Task GetTicketByIdAsync_ThrowsUnauthorized_WhenUserNotMember()
    {
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        _ticketIdParsingService.Parse(ticketId.ToString()).Returns(new TicketIdParseResult(TicketIdType.Internal, ticketId, null, null));
        var ticket = Orchestra.Application.Tests.Builders.TicketBuilder.InternalTicket();
        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuth.IsMemberAsync(userId, ticket.WorkspaceId, Arg.Any<CancellationToken>()).Returns(false);
        var service = CreateService();
        await Assert.ThrowsAsync<UnauthorizedTicketAccessException>(() => service.GetTicketByIdAsync(ticketId.ToString(), userId));
    }

    [Fact]
    public async Task GetTicketByIdAsync_HandlesMaterializedExternalTicket_RedirectsToComposite()
    {
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var extId = "EXT-123";
        _ticketIdParsingService.Parse(ticketId.ToString()).Returns(new TicketIdParseResult(TicketIdType.Internal, ticketId, null, null));
        var ticket = new Orchestra.Application.Tests.Builders.TicketBuilder()
            .WithId(ticketId)
            .WithWorkspaceId(Guid.NewGuid())
            .AsExternal(integrationId, extId)
            .Build();
        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuth.IsMemberAsync(userId, ticket.WorkspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        // Should call GetExternalTicketByCompositeIdAsync, which will throw unless further stubbed
        var service = CreateService();
        await Assert.ThrowsAnyAsync<Exception>(() => service.GetTicketByIdAsync(ticketId.ToString(), userId));
    }

    [Fact]
    public async Task GetTicketsAsync_DeduplicatesTicketsById()
    {
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupPaginationService();
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        var ticketId = Guid.NewGuid();
        var ticket = new Orchestra.Application.Tests.Builders.TicketBuilder()
            .WithId(ticketId)
            .WithWorkspaceId(workspaceId)
            .WithTitle("T1")
            .AsInternal()
            .Build();
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 50, Arg.Any<CancellationToken>()).Returns(new List<Ticket> { ticket, ticket });
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration>());
        var service = CreateService();
        var result = await service.GetTicketsAsync(workspaceId, userId);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetTicketsAsync_SentimentServiceThrows_DefaultsTo100()
    {
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupPaginationService();
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        var ticket = new Orchestra.Application.Tests.Builders.TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .WithTitle("T1")
            .AsInternal()
            .Build();
        var comment = new Orchestra.Application.Tests.Builders.TicketCommentBuilder()
            .WithTicketId(ticket.Id)
            .WithAuthor("a")
            .WithContent("good")
            .Build();
        // Add comment to ticket's Comments collection via reflection (since Comments is private set)
        var commentsProp = typeof(Ticket).GetProperty("Comments");
        var comments = (List<TicketComment>)commentsProp.GetValue(ticket);
        comments.Add(comment);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 50, Arg.Any<CancellationToken>()).Returns(new List<Ticket> { ticket });
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration>());
        _sentimentAnalysisService.AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<CancellationToken>()).Returns<Task<List<TicketSentimentResult>>>(_ => throw new Exception("Sentiment error"));
        var service = CreateService();
        var result = await service.GetTicketsAsync(workspaceId, userId);
        Assert.Equal(100, result.Items[0].Satisfaction);
    }

    [Fact]
    public async Task GetAllStatusesAsync_ReturnsEmptyList_WhenNoStatuses()
    {
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        var service = CreateService();
        var result = await service.GetAllStatusesAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllPrioritiesAsync_ReturnsEmptyList_WhenNoPriorities()
    {
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        var service = CreateService();
        var result = await service.GetAllPrioritiesAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTicketsAsync_ThrowsIfWorkspaceIdEmpty()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTicketsAsync(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetTicketsAsync_ThrowsIfUserIdEmpty()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTicketsAsync(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public async Task GetTicketByIdAsync_ThrowsIfTicketIdEmpty()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTicketByIdAsync("", Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAllStatusesAsync_ReturnsMappedDtos()
    {
        var statuses = new List<TicketStatus> { new() { Id = Guid.NewGuid(), Name = "Open", Color = "green" } };
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(statuses);
        var service = CreateService();
        var result = await service.GetAllStatusesAsync();
        Assert.Single(result);
        Assert.Equal("Open", result[0].Name);
    }

    [Fact]
    public async Task GetAllPrioritiesAsync_ReturnsMappedDtos()
    {
        var priorities = new List<TicketPriority> { new() { Id = Guid.NewGuid(), Name = "High", Color = "red", Value = 1 } };
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(priorities);
        var service = CreateService();
        var result = await service.GetAllPrioritiesAsync();
        Assert.Single(result);
        Assert.Equal("High", result[0].Name);
    }
    [Fact]
    public async Task GetTicketsAsync_Pagination_1Internal7External_Limit5()
    {
        // Case 1: 1 internal + 7 external, limit 5
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        // Setup pagination service to simulate phase/offset changes
        int parsePageTokenCall = 0;
        _ticketPaginationService.ParsePageToken(Arg.Any<string>()).Returns(ci =>
        {
            parsePageTokenCall++;
            if (parsePageTokenCall == 1)
                return new TicketPageToken { Phase = "internal", InternalOffset = 0 };
            else
                return new TicketPageToken { Phase = "external", InternalOffset = 1 };
        });
        _ticketPaginationService.NormalizePageSize(Arg.Any<int>()).Returns(x => (int)x[0]);
        _ticketPaginationService.SerializePageToken(Arg.Any<TicketPageToken>()).Returns("next-token");
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        // 1 internal ticket
        var internalTicket = new Orchestra.Application.Tests.Builders.TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .WithTitle("Internal")
            .AsInternal()
            .Build();
        // 7 external tickets (materialized)
        var externalTickets = Enumerable.Range(1, 7).Select(i =>
            new Orchestra.Application.Tests.Builders.TicketBuilder()
                .WithId(Guid.NewGuid())
                .WithWorkspaceId(workspaceId)
                .AsExternal(integrationId, $"EXT-{i}")
                .Build()
        ).ToList();
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 5, Arg.Any<CancellationToken>()).Returns(new List<Ticket> { internalTicket });
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 1, 5, Arg.Any<CancellationToken>()).Returns(new List<Ticket>());
        // Do not return materialized tickets here; only internal tickets are returned by GetInternalTicketsByWorkspaceAsync
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        var integration = new Orchestra.Application.Tests.Builders.IntegrationBuilder().WithId(integrationId).WithWorkspaceId(workspaceId).Build();
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration> { integration });
        var externalTicketDtos = Enumerable.Range(1, 7).Select(i => new Orchestra.Application.Tickets.DTOs.ExternalTicketDto(
            integrationId,
            $"EXT-{i}",
            $"External {i}",
            "desc",
            "status",
            "#000",
            "prio",
            "#111",
            1,
            "http://external.url",
            new List<Orchestra.Application.Tickets.DTOs.CommentDto>()
        )).ToList();
        var externalTicketDtosAsTicketDtos = externalTicketDtos.Select(e => new Orchestra.Application.Tickets.DTOs.TicketDto(
            $"{e.IntegrationId}:{e.ExternalTicketId}",
            workspaceId,
            e.Title,
            e.Description,
            null,
            null,
            false,
            e.IntegrationId,
            e.ExternalTicketId,
            e.ExternalUrl,
            "JIRA",
            null,
            null,
            e.Comments,
            null,
            null
        )).ToList();
        int fetchCallCount = 0;
        _externalFetchingService.FetchExternalTicketsAsync(
            Arg.Any<List<Integration>>(),
            Arg.Any<int>(),
            null,
            Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                var limit = callInfo.ArgAt<int>(1);
                List<Orchestra.Application.Tickets.DTOs.TicketDto> page;
                bool hasMore;
                if (fetchCallCount == 0) {
                    // First call: return first 4 external, more remain
                    page = externalTicketDtosAsTicketDtos.Take(4).ToList();
                    hasMore = true;
                } else {
                    // Second call: return remaining 3 external, no more remain
                    page = externalTicketDtosAsTicketDtos.Skip(4).Take(3).ToList();
                    hasMore = false;
                }
                fetchCallCount++;
                return Task.FromResult((page, hasMore, (Orchestra.Application.Common.Interfaces.ExternalPaginationState)null));
            });
        var service = CreateService();
        // First call
        var result1 = await service.GetTicketsAsync(workspaceId, userId, null, 5);
        Assert.Equal(5, result1.Items.Count);
        Assert.Equal("Internal", result1.Items[0].Title); // Internal ticket first
        Assert.All(result1.Items.Skip(1), t => Assert.StartsWith("External", t.Title));
        Assert.False(result1.IsLast);
        // Second call (should return remaining 3 external)
        var result2 = await service.GetTicketsAsync(workspaceId, userId, result1.NextPageToken, 5);
        Assert.Equal(3, result2.Items.Count);
        Assert.All(result2.Items, t => Assert.StartsWith("External", t.Title));
        Assert.True(result2.IsLast);
    }

    [Fact]
    public async Task GetTicketsAsync_Pagination_11Internal9External_Limit5()
    {
        // Case 2: 11 internal + 9 external, limit 5
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        // Setup pagination service to simulate phase/offset changes
        int parsePageTokenCall = 0;
        _ticketPaginationService.ParsePageToken(Arg.Any<string>()).Returns(ci =>
        {
            parsePageTokenCall++;
            if (parsePageTokenCall == 1)
                return new TicketPageToken { Phase = "internal", InternalOffset = 0 };
            else if (parsePageTokenCall == 2)
                return new TicketPageToken { Phase = "internal", InternalOffset = 5 };
            else if (parsePageTokenCall == 3)
                return new TicketPageToken { Phase = "internal", InternalOffset = 10 };
            else
                return new TicketPageToken { Phase = "external", InternalOffset = 10 };
        });
        _ticketPaginationService.NormalizePageSize(Arg.Any<int>()).Returns(x => (int)x[0]);
        _ticketPaginationService.SerializePageToken(Arg.Any<TicketPageToken>()).Returns("next-token");
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        // 11 internal tickets
        var internalTickets = Enumerable.Range(1, 11).Select(i =>
            new Orchestra.Application.Tests.Builders.TicketBuilder()
                .WithId(Guid.NewGuid())
                .WithWorkspaceId(workspaceId)
                .WithTitle($"Internal {i}")
                .AsInternal()
                .Build()
        ).ToList();
        // 9 external tickets (materialized)
        var externalTickets = Enumerable.Range(1, 9).Select(i =>
            new Orchestra.Application.Tests.Builders.TicketBuilder()
                .WithId(Guid.NewGuid())
                .WithWorkspaceId(workspaceId)
                .AsExternal(integrationId, $"EXT-{i}")
                .Build()
        ).ToList();
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 5, Arg.Any<CancellationToken>()).Returns(internalTickets.Take(5).ToList());
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 5, 5, Arg.Any<CancellationToken>()).Returns(internalTickets.Skip(5).Take(5).ToList());
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 10, 5, Arg.Any<CancellationToken>()).Returns(internalTickets.Skip(10).Take(5).ToList());
        // Do not return materialized tickets here; only internal tickets are returned by GetInternalTicketsByWorkspaceAsync
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        var integration = new Orchestra.Application.Tests.Builders.IntegrationBuilder().WithId(integrationId).WithWorkspaceId(workspaceId).Build();
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration> { integration });
        var externalTicketDtos = Enumerable.Range(1, 9).Select(i => new Orchestra.Application.Tickets.DTOs.ExternalTicketDto(
            integrationId,
            $"EXT-{i}",
            $"External {i}",
            "desc",
            "status",
            "#000",
            "prio",
            "#111",
            1,
            "http://external.url",
            new List<Orchestra.Application.Tickets.DTOs.CommentDto>()
        )).ToList();
        var externalTicketDtosAsTicketDtos = externalTicketDtos.Select(e => new Orchestra.Application.Tickets.DTOs.TicketDto(
            $"{e.IntegrationId}:{e.ExternalTicketId}",
            workspaceId,
            e.Title,
            e.Description,
            null,
            null,
            false,
            e.IntegrationId,
            e.ExternalTicketId,
            e.ExternalUrl,
            "JIRA",
            null,
            null,
            e.Comments,
            null,
            null
        )).ToList();
        _externalFetchingService.FetchExternalTicketsAsync(
            Arg.Any<List<Integration>>(),
            Arg.Any<int>(),
            null,
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((externalTicketDtosAsTicketDtos, false, (Orchestra.Application.Common.Interfaces.ExternalPaginationState)null)));
        var service = CreateService();
        // First call
        var result1 = await service.GetTicketsAsync(workspaceId, userId, null, 5);
        Assert.Equal(5, result1.Items.Count);
        Assert.All(result1.Items, t => Assert.StartsWith("Internal", t.Title));
        Assert.False(result1.IsLast);
        // Second call
        var result2 = await service.GetTicketsAsync(workspaceId, userId, result1.NextPageToken, 5);
        Assert.Equal(5, result2.Items.Count);
        Assert.All(result2.Items, t => Assert.StartsWith("Internal", t.Title));
        Assert.False(result2.IsLast);
        // Third call (should return 1 internal and 9 external)
        var result3 = await service.GetTicketsAsync(workspaceId, userId, result2.NextPageToken, 5);
        Assert.Equal(10, result3.Items.Count);
        Assert.StartsWith("Internal", result3.Items[0].Title);
        Assert.All(result3.Items.Skip(1), t => Assert.StartsWith("External", t.Title));
        Assert.True(result3.IsLast);
    }
}
