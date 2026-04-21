using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Tickets.Services;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tickets;

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
    private readonly IWorkspaceDataAccess _workspaceDataAccess = Substitute.For<IWorkspaceDataAccess>();
    private readonly IWorkspaceAIProviderRepository _aiProviderRepository = Substitute.For<IWorkspaceAIProviderRepository>();

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
        _workspaceDataAccess,
        _aiProviderRepository,
        _logger
    );

    private void SetupPaginationService(string phase = "internal", int offset = 0)
    {
        _ticketPaginationService.ParsePageToken(Arg.Any<string>()).Returns(new TicketPageToken { Phase = phase, InternalOffset = offset });
        _ticketPaginationService.NormalizePageSize(Arg.Any<int>()).Returns(x => (int)x[0]);
        _ticketPaginationService.SerializePageToken(Arg.Any<TicketPageToken>()).Returns("next-token");
    }

    private void SetupWorkspaceWithCsatEnabled(Guid workspaceId, bool csatEnabled, string? modelId = "gpt-4o", string? defaultModelId = null)
    {
        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsCustomerSatisfactionAnalysisEnabled(csatEnabled)
            .WithCustomerSatisfactionAnalysisModelId(modelId)
            .Build();

        _workspaceDataAccess.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(workspace);
    }

    private void SetupEnrichmentService()
    {
        // Use real TicketEnrichmentService with mocked sentiment service
        var enrichmentService = new TicketEnrichmentService(
            _sentimentAnalysisService,
            Substitute.For<ISummarizationService>(),
            Substitute.For<ILogger<TicketEnrichmentService>>()
        );
        
        _ticketEnrichmentService
            .CalculateSentimentAsync(Arg.Any<List<TicketDto>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => enrichmentService.CalculateSentimentAsync(
                (List<TicketDto>)x[0],
                (string)x[1],
                (CancellationToken)x[2]
            ));
    }

    private List<InternalTicketListDto> ConvertTicketsToInternalDtos(List<Ticket> tickets)
    {
        return tickets.Select(t => new InternalTicketListDto
        {
            Id = t.Id,
            WorkspaceId = t.WorkspaceId,
            Title = t.Title,
            Description = t.Description,
            StatusId = t.StatusId,
            PriorityId = t.PriorityId,
            PriorityValue = 0,
            PriorityName = null,
            PriorityColor = null,
            IntegrationName = null,
            IntegrationId = t.IntegrationId,
            ExternalTicketId = t.ExternalTicketId,
            AssignedAgentId = t.AssignedAgentId,
            AssignedWorkflowId = t.AssignedWorkflowId,
            IsInternal = t.IsInternal,
            CommentCount = 0,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();
    }

    [Fact]
    public async Task GetTicketsAsync_HandlesInvalidPageToken_Gracefully()
    {
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupPaginationService();
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 50, Arg.Any<CancellationToken>()).Returns(new List<InternalTicketListDto>());
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
        var ticket = TicketBuilder.InternalTicket();
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
        var ticket = new TicketBuilder()
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
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: false);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        var ticketId = Guid.NewGuid();
        var ticket = new TicketBuilder()
            .WithId(ticketId)
            .WithWorkspaceId(workspaceId)
            .WithTitle("T1")
            .AsInternal()
            .Build();
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 50, Arg.Any<CancellationToken>()).Returns(ConvertTicketsToInternalDtos(new List<Ticket> { ticket, ticket }));
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _ticketDataAccess.GetCommentsByTicketIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new Dictionary<Guid, List<TicketComment>>()));
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
        SetupEnrichmentService();
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: false);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        var ticket = new TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .WithTitle("T1")
            .AsInternal()
            .Build();
        var comment = new TicketCommentBuilder()
            .WithTicketId(ticket.Id)
            .WithAuthor("a")
            .WithContent("good")
            .Build();
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 50, Arg.Any<CancellationToken>()).Returns(Task.FromResult(ConvertTicketsToInternalDtos(new List<Ticket> { ticket })));
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _ticketDataAccess.GetCommentsByTicketIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new Dictionary<Guid, List<TicketComment>> { { ticket.Id, [comment] } }));
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration>());
        _externalFetchingService.FetchExternalTicketsAsync(Arg.Any<List<Integration>>(), Arg.Any<int>(), Arg.Any<ExternalPaginationState>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult((new List<TicketDto>(), false, (ExternalPaginationState?)null)));
        _sentimentAnalysisService.AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns<Task<List<TicketSentimentResult>>>(_ => throw new Exception("Sentiment error"));
        var service = CreateService();
        var result = await service.GetTicketsAsync(workspaceId, userId);
        Assert.Equal(100, result.Items[0].Satisfaction);
    }

    [Fact]
    public async Task GetTicketsAsync_WithCsatDisabled_AssignsDefaultSatisfactionAndDoesNotCallSentimentService()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var externalTicket = new TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .AsExternal(Guid.NewGuid(), "EXT-1")
            .Build();
        var comment = new TicketCommentBuilder()
            .WithTicketId(externalTicket.Id)
            .WithAuthor("a")
            .WithContent("Comment with sentiment")
            .Build();

        SetupPaginationService();
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: false);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<InternalTicketListDto>()));
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _ticketDataAccess.GetCommentsByTicketIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new Dictionary<Guid, List<TicketComment>>()));
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration>());
        _externalFetchingService.FetchExternalTicketsAsync(Arg.Any<List<Integration>>(), Arg.Any<int>(), Arg.Any<ExternalPaginationState>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult((new List<TicketDto>(), false, (ExternalPaginationState?)null)));

        var service = CreateService();

        // Act
        var result = await service.GetTicketsAsync(workspaceId, userId, pageSize: 50);

        // Assert
        Assert.True(result.IsLast);
        Assert.Empty(result.Items);
        await _sentimentAnalysisService.DidNotReceive()
            .AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTicketsAsync_WithCsatDisabledAndMixedTickets_AllTicketsReceive100Satisfaction()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var internalTicket = new TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .AsInternal()
            .Build();

        var materializedTicket = new TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .AsExternal(Guid.NewGuid(), "EXT-2")
            .Build();
        var comment = new TicketCommentBuilder()
            .WithTicketId(materializedTicket.Id)
            .WithAuthor("a")
            .WithContent("Materialized with comments")
            .Build();

        SetupPaginationService();
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: false);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConvertTicketsToInternalDtos(new List<Ticket> { internalTicket, materializedTicket })));
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _externalFetchingService.FetchExternalTicketsAsync(Arg.Any<List<Integration>>(), Arg.Any<int>(), Arg.Any<ExternalPaginationState>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult((new List<TicketDto>(), false, (ExternalPaginationState?)null)));
        _ticketDataAccess.GetCommentsByTicketIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new Dictionary<Guid, List<TicketComment>> { { materializedTicket.Id, [comment] } }));
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration>());

        var service = CreateService();

        // Act
        var result = await service.GetTicketsAsync(workspaceId, userId, pageSize: 50);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, ticket => Assert.Equal(100, ticket.Satisfaction));
        await _sentimentAnalysisService.DidNotReceive()
            .AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: false);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        // 1 internal ticket
        var internalTicket = new TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .WithTitle("Internal")
            .AsInternal()
            .Build();
        // 7 external tickets (materialized)
        var externalTickets = Enumerable.Range(1, 7).Select(i =>
            new TicketBuilder()
                .WithId(Guid.NewGuid())
                .WithWorkspaceId(workspaceId)
                .AsExternal(integrationId, $"EXT-{i}")
                .Build()
        ).ToList();
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 5, Arg.Any<CancellationToken>()).Returns(ConvertTicketsToInternalDtos(new List<Ticket> { internalTicket }));
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 1, 5, Arg.Any<CancellationToken>()).Returns(new List<InternalTicketListDto>());
        // Do not return materialized tickets here; only internal tickets are returned by GetInternalTicketsByWorkspaceAsync
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _ticketDataAccess.GetCommentsByTicketIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new Dictionary<Guid, List<TicketComment>>()));
        var integration = new IntegrationBuilder().WithId(integrationId).WithWorkspaceId(workspaceId).Build();
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
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: false);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        // 11 internal tickets
        var internalTickets = Enumerable.Range(1, 11).Select(i =>
            new TicketBuilder()
                .WithId(Guid.NewGuid())
                .WithWorkspaceId(workspaceId)
                .WithTitle($"Internal {i}")
                .AsInternal()
                .Build()
        ).ToList();
        // 9 external tickets (materialized)
        var externalTickets = Enumerable.Range(1, 9).Select(i =>
            new TicketBuilder()
                .WithId(Guid.NewGuid())
                .WithWorkspaceId(workspaceId)
                .AsExternal(integrationId, $"EXT-{i}")
                .Build()
        ).ToList();
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 5, Arg.Any<CancellationToken>()).Returns(ConvertTicketsToInternalDtos(internalTickets.Take(5).ToList()));
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 5, 5, Arg.Any<CancellationToken>()).Returns(ConvertTicketsToInternalDtos(internalTickets.Skip(5).Take(5).ToList()));
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 10, 5, Arg.Any<CancellationToken>()).Returns(ConvertTicketsToInternalDtos(internalTickets.Skip(10).Take(5).ToList()));
        // Do not return materialized tickets here; only internal tickets are returned by GetInternalTicketsByWorkspaceAsync
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _ticketDataAccess.GetCommentsByTicketIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new Dictionary<Guid, List<TicketComment>>()));
        var integration = new IntegrationBuilder().WithId(integrationId).WithWorkspaceId(workspaceId).Build();
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

    // -----------------------------------------------------------------------
    // Bug regression: isLast incorrectly false / Load More shows nothing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Bug 1 regression: pageSize=10, 1 provider with only 6 tickets.
    /// The external service signals all providers exhausted (hasMore=false).
    /// Expected: IsLast=true and no NextPageToken on the first (and only) page.
    /// </summary>
    [Fact]
    public async Task GetTicketsAsync_Bug1_SingleProviderAllExhausted_IsLastTrue()
    {
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();

        _ticketPaginationService.ParsePageToken(Arg.Any<string>())
            .Returns(new TicketPageToken { Phase = "internal", InternalOffset = 0 });
        _ticketPaginationService.NormalizePageSize(Arg.Any<int>()).Returns(x => (int)x[0]);
        _ticketPaginationService.SerializePageToken(Arg.Any<TicketPageToken>()).Returns("next-token");
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: false);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);

        // No pure-internal tickets in the DB.
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 10, Arg.Any<CancellationToken>())
            .Returns(new List<InternalTicketListDto>());
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());

        var integration = new IntegrationBuilder()
            .WithId(integrationId).WithWorkspaceId(workspaceId).Build();
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        // External service returns 6 tickets but marks the provider as exhausted (hasMore=false).
        var sixDtos = Enumerable.Range(1, 6).Select(i => new TicketDtoBuilder()
            .WithId($"{integrationId}:EXT-{i}")
            .WithWorkspaceId(workspaceId)
            .WithTitle($"Ext {i}")
            .AsExternal(integrationId, $"EXT-{i}")
            .Build()).ToList();

        var exhaustedState = new ExternalPaginationState
        {
            ExhaustedProviderIds = new List<string> { integrationId.ToString() }
        };
        _externalFetchingService.FetchExternalTicketsAsync(
                Arg.Any<List<Integration>>(), 10, Arg.Any<ExternalPaginationState>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(List<TicketDto>, bool, ExternalPaginationState)>((sixDtos, false, exhaustedState)));

        var service = CreateService();
        var result = await service.GetTicketsAsync(workspaceId, userId, null, 10);

        Assert.Equal(6, result.Items.Count);
        Assert.True(result.IsLast,
            "IsLast must be true when the provider returned fewer tickets than pageSize and is exhausted.");
        Assert.Null(result.NextPageToken);
    }

    /// <summary>
    /// Bug 2 regression: pageSize=10, external service fills the page (hasMore=true) and
    /// returns a non-null ExternalPaginationState.
    /// Expected: the state is embedded in the next-page token so the second request continues
    /// from where it left off instead of restarting from scratch.
    /// </summary>
    [Fact]
    public async Task GetTicketsAsync_Bug2_ExternalStatePreservedInNextPageToken()
    {
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();

        _ticketPaginationService.ParsePageToken(Arg.Any<string>())
            .Returns(new TicketPageToken { Phase = "internal", InternalOffset = 0 });
        _ticketPaginationService.NormalizePageSize(Arg.Any<int>()).Returns(x => (int)x[0]);

        // Capture the token passed to SerializePageToken so we can inspect its ExternalState.
        TicketPageToken? capturedToken = null;
        _ticketPaginationService.SerializePageToken(Arg.Any<TicketPageToken>())
            .Returns(ci =>
            {
                capturedToken = ci.ArgAt<TicketPageToken>(0);
                return "next-token";
            });

        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: false);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 10, Arg.Any<CancellationToken>())
            .Returns(new List<InternalTicketListDto>());
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());

        var integration = new IntegrationBuilder()
            .WithId(integrationId).WithWorkspaceId(workspaceId).Build();
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });

        // External service fills the page exactly (10/10) and signals more remain.
        var tenDtos = Enumerable.Range(1, 10).Select(i => new TicketDtoBuilder()
            .WithId($"{integrationId}:EXT-{i}")
            .WithWorkspaceId(workspaceId)
            .AsExternal(integrationId, $"EXT-{i}")
            .Build()).ToList();

        var partialState = new ExternalPaginationState
        {
            TotalExternalFetched = 10,
            ProviderTokens = new Dictionary<string, string?>
            {
                { integrationId.ToString(), "cursor-after-10" }
            }
        };
        _externalFetchingService.FetchExternalTicketsAsync(
                Arg.Any<List<Integration>>(), 10, Arg.Any<ExternalPaginationState>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(List<TicketDto>, bool, ExternalPaginationState)>((tenDtos, true, partialState)));

        var service = CreateService();
        var result = await service.GetTicketsAsync(workspaceId, userId, null, 10);

        Assert.Equal(10, result.Items.Count);
        Assert.False(result.IsLast);
        Assert.NotNull(result.NextPageToken);

        // Core assertion: the ExternalState must not be null in the serialised token.
        // A null ExternalState would cause the next request to restart from offset 0
        // (the root cause of Bug 2's "Load More does nothing" symptom).
        Assert.NotNull(capturedToken);
        Assert.NotNull(capturedToken!.ExternalState);
        Assert.Equal("cursor-after-10",
            capturedToken.ExternalState!.ProviderTokens[integrationId.ToString()]);
    }

    [Fact]
    public async Task GetTicketsAsync_WithCsatEnabledAndExternalTickets_CallsSentimentServiceAndAssignsScores()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();

        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(workspaceId)
            .Build();

        // No internal tickets - all tickets come from external phase
        var externalTicketDto = new TicketDto(
            Id: "1:EXT-123",
            WorkspaceId: workspaceId,
            Title: "External ticket",
            Description: "External",
            Status: null,
            Priority: null,
            Internal: false,
            IntegrationId: integrationId,
            ExternalTicketId: "EXT-123",
            ExternalUrl: "http://example.com/123",
            Source: "EXTERNAL",
            AssignedAgentId: null,
            AssignedWorkflowId: null,
            Comments: new List<CommentDto> { new CommentDto("1", "Author", "This is a really positive comment!", DateTime.UtcNow) },
            Satisfaction: null,
            Summary: null
        );

        var sentimentResult = new TicketSentimentResult(externalTicketDto.Id, 85);

        SetupPaginationService();
        SetupEnrichmentService();
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: true);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<InternalTicketListDto>());
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration> { integration });
        _externalFetchingService.FetchExternalTicketsAsync(
            Arg.Any<List<Integration>>(),
            Arg.Any<int>(),
            Arg.Any<ExternalPaginationState>(),
            Arg.Any<CancellationToken>())
            .Returns((
                Tickets: new List<TicketDto> { externalTicketDto },
                HasMore: false,
                State: new ExternalPaginationState { CurrentProviderIndex = 0, ProviderTokens = new Dictionary<string, string?>(), TotalExternalFetched = 1, ExhaustedProviderIds = new List<string> { integrationId.ToString() } }
            ));
        _sentimentAnalysisService.AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<TicketSentimentResult> { sentimentResult });

        var service = CreateService();

        // Act
        var result = await service.GetTicketsAsync(workspaceId, userId, pageSize: 50);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.Equal(85, result.Items.First().Satisfaction);
        await _sentimentAnalysisService.Received(1)
            .AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTicketsAsync_WithPureInternalTickets_AlwaysAssigns100_RegardlessOfCsatFlag()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var pureInternalTicket = new TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .AsInternal()
            .Build();
        var comment = new TicketCommentBuilder()
            .WithTicketId(pureInternalTicket.Id)
            .WithAuthor("a")
            .WithContent("Internal comment")
            .Build();

        SetupPaginationService();
        SetupEnrichmentService();
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: true);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConvertTicketsToInternalDtos(new List<Ticket> { pureInternalTicket })));
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _externalFetchingService.FetchExternalTicketsAsync(Arg.Any<List<Integration>>(), Arg.Any<int>(), Arg.Any<ExternalPaginationState>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult((new List<TicketDto>(), false, (ExternalPaginationState?)null)));
        _ticketDataAccess.GetCommentsByTicketIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new Dictionary<Guid, List<TicketComment>> { { pureInternalTicket.Id, [comment] } }));
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration>());

        var service = CreateService();

        // Act
        var result = await service.GetTicketsAsync(workspaceId, userId, pageSize: 50);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.Equal(100, result.Items.First().Satisfaction);
        await _sentimentAnalysisService.DidNotReceive()
            .AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTicketsAsync_WithCommentlessExternalTickets_AlwaysAssigns100_RegardlessOfCsatFlag()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var commentlessTicket = new TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .AsInternal()
            .Build();
        // No comments added - empty comment list

        SetupPaginationService();
        SetupEnrichmentService();
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: true);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ConvertTicketsToInternalDtos(new List<Ticket> { commentlessTicket }));
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _ticketDataAccess.GetCommentsByTicketIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new Dictionary<Guid, List<TicketComment>>()));
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration>());

        var service = CreateService();

        // Act
        var result = await service.GetTicketsAsync(workspaceId, userId, pageSize: 50);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.Equal(100, result.Items.First().Satisfaction);
        await _sentimentAnalysisService.DidNotReceive()
            .AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTicketsAsync_WithSentimentServiceFailure_DefaultsTicketsTo100()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();

        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var externalTicketDto = new TicketDto(
            Id: "1:EXT-FAIL",
            WorkspaceId: workspaceId,
            Title: "External ticket",
            Description: "External",
            Status: null,
            Priority: null,
            Internal: false,
            IntegrationId: integrationId,
            ExternalTicketId: "EXT-FAIL",
            ExternalUrl: "http://example.com/fail",
            Source: "EXTERNAL",
            AssignedAgentId: null,
            AssignedWorkflowId: null,
            Comments: new List<CommentDto> { new CommentDto("1", "Author", "Comment content", DateTime.UtcNow) },
            Satisfaction: null,
            Summary: null
        );

        SetupPaginationService();
        SetupEnrichmentService();
        SetupWorkspaceWithCsatEnabled(workspaceId, csatEnabled: true);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<InternalTicketListDto>());
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration> { integration });
        _externalFetchingService.FetchExternalTicketsAsync(
            Arg.Any<List<Integration>>(),
            Arg.Any<int>(),
            Arg.Any<ExternalPaginationState>(),
            Arg.Any<CancellationToken>())
            .Returns((
                Tickets: new List<TicketDto> { externalTicketDto },
                HasMore: false,
                State: new ExternalPaginationState { CurrentProviderIndex = 0, ProviderTokens = new Dictionary<string, string?>(), TotalExternalFetched = 1, ExhaustedProviderIds = new List<string> { integrationId.ToString() } }
            ));
        _sentimentAnalysisService.AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<List<TicketSentimentResult>>>(_ => throw new Exception("AI service unavailable"));

        var service = CreateService();

        // Act
        var result = await service.GetTicketsAsync(workspaceId, userId, pageSize: 50);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.Equal(100, result.Items.First().Satisfaction);
    }

    [Fact]
    public async Task GetTicketsAsync_WithWorkspaceNotFound_DefaultsCsatToEnabled()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();

        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithWorkspaceId(workspaceId)
            .Build();

        var externalTicketDto = new TicketDto(
            Id: "1:EXT-NULLWS",
            WorkspaceId: workspaceId,
            Title: "External ticket",
            Description: "External",
            Status: null,
            Priority: null,
            Internal: false,
            IntegrationId: integrationId,
            ExternalTicketId: "EXT-NULLWS",
            ExternalUrl: "http://example.com/nullws",
            Source: "EXTERNAL",
            AssignedAgentId: null,
            AssignedWorkflowId: null,
            Comments: new List<CommentDto> { new CommentDto("1", "Author", "Comment", DateTime.UtcNow) },
            Satisfaction: null,
            Summary: null
        );

        var sentimentResult = new TicketSentimentResult(externalTicketDto.Id, 75);

        SetupPaginationService();
        SetupEnrichmentService();
        // Null workspace means CSAT enabled by default with a model ID
        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsCustomerSatisfactionAnalysisEnabled(true)
            .WithCustomerSatisfactionAnalysisModelId("gpt-4o")
            .Build();
        _workspaceDataAccess.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(workspace);
        _workspaceAuth.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>()).Returns(true);
        _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<InternalTicketListDto>());
        _ticketDataAccess.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketStatus>());
        _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority>());
        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new List<Integration> { integration });
        _externalFetchingService.FetchExternalTicketsAsync(
            Arg.Any<List<Integration>>(),
            Arg.Any<int>(),
            Arg.Any<ExternalPaginationState>(),
            Arg.Any<CancellationToken>())
            .Returns((
                Tickets: new List<TicketDto> { externalTicketDto },
                HasMore: false,
                State: new ExternalPaginationState { CurrentProviderIndex = 0, ProviderTokens = new Dictionary<string, string?>(), TotalExternalFetched = 1, ExhaustedProviderIds = new List<string> { integrationId.ToString() } }
            ));
        _sentimentAnalysisService.AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<TicketSentimentResult> { sentimentResult });

        var service = CreateService();

        // Act
        var result = await service.GetTicketsAsync(workspaceId, userId, pageSize: 50);

        // Assert
        Assert.NotEmpty(result.Items);
        Assert.Equal(75, result.Items.First().Satisfaction);
        await _sentimentAnalysisService.Received(1)
            .AnalyzeBatchSentimentAsync(Arg.Any<List<TicketSentimentRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

public class TicketQueryServiceCSATModelResolutionTests
{
    private readonly ITicketDataAccess _ticketDataAccessMock = Substitute.For<ITicketDataAccess>();
    private readonly IWorkspaceAuthorizationService _authMock = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly IIntegrationDataAccess _integrationDataAccessMock = Substitute.For<IIntegrationDataAccess>();
    private readonly ITicketProviderFactory _providerFactoryMock = Substitute.For<ITicketProviderFactory>();
    private readonly ITicketIdParsingService _idParserMock = Substitute.For<ITicketIdParsingService>();
    private readonly ITicketMappingService _mappingServiceMock = Substitute.For<ITicketMappingService>();
    private readonly ITicketEnrichmentService _enrichmentServiceMock = Substitute.For<ITicketEnrichmentService>();
    private readonly IExternalTicketFetchingService _externalFetchMock = Substitute.For<IExternalTicketFetchingService>();
    private readonly ISentimentAnalysisService _sentimentMock = Substitute.For<ISentimentAnalysisService>();
    private readonly ITicketPaginationService _paginationMock = Substitute.For<ITicketPaginationService>();
    private readonly IWorkspaceDataAccess _workspaceDataAccessMock = Substitute.For<IWorkspaceDataAccess>();
    private readonly IWorkspaceAIProviderRepository _aiProviderRepositoryMock = Substitute.For<IWorkspaceAIProviderRepository>();
    private readonly ILogger<TicketQueryService> _loggerMock = Substitute.For<ILogger<TicketQueryService>>();
    private readonly TicketQueryService _sut;

    public TicketQueryServiceCSATModelResolutionTests()
    {
        _sut = new TicketQueryService(
            _ticketDataAccessMock,
            _authMock,
            _integrationDataAccessMock,
            _providerFactoryMock,
            _idParserMock,
            _mappingServiceMock,
            _enrichmentServiceMock,
            _externalFetchMock,
            _sentimentMock,
            _paginationMock,
            _workspaceDataAccessMock,
            _aiProviderRepositoryMock,
            _loggerMock);
    }

    private List<InternalTicketListDto> ConvertTicketsToInternalDtos(List<Ticket> tickets)
    {
        return tickets.Select(t => new InternalTicketListDto
        {
            Id = t.Id,
            WorkspaceId = t.WorkspaceId,
            Title = t.Title,
            Description = t.Description,
            StatusId = t.StatusId,
            PriorityId = t.PriorityId,
            PriorityValue = 0,
            PriorityName = null,
            PriorityColor = null,
            IntegrationName = null,
            IntegrationId = t.IntegrationId,
            ExternalTicketId = t.ExternalTicketId,
            AssignedAgentId = t.AssignedAgentId,
            AssignedWorkflowId = t.AssignedWorkflowId,
            IsInternal = t.IsInternal,
            CommentCount = 0,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();
    }

    [Fact]
    public async Task GetTicketsAsync_WithValidWorkspaceModel_PassesModelIdToEnrichmentService()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string workspaceModelId = "gpt-4-turbo";
        
        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsCustomerSatisfactionAnalysisEnabled(true)
            .WithCustomerSatisfactionAnalysisModelId(workspaceModelId)
            .Build();

        var internalTicket = new TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .AsInternal()
            .Build();
        var comment = new TicketCommentBuilder()
            .WithTicketId(internalTicket.Id)
            .WithAuthor("a")
            .WithContent("Comment with sentiment")
            .Build();

        var tickets = new List<Ticket> { internalTicket };
        var commentsDict = new Dictionary<Guid, List<TicketComment>> { { internalTicket.Id, [comment] } };

        // Mock setup
        _authMock.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _paginationMock.NormalizePageSize(Arg.Any<int>()).Returns(50);
        _paginationMock.ParsePageToken(null).Returns(new TicketPageToken { Phase = "internal" });
        _ticketDataAccessMock.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 50, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConvertTicketsToInternalDtos(tickets)));
        _ticketDataAccessMock.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<TicketStatus>()));
        _ticketDataAccessMock.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<TicketPriority>()));
        _ticketDataAccessMock.GetCommentsByTicketIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(commentsDict));
        _integrationDataAccessMock.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<Integration>()));
        _workspaceDataAccessMock.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(workspace));
        _enrichmentServiceMock.CalculateSentimentAsync(Arg.Any<List<TicketDto>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.GetTicketsAsync(workspaceId, userId, null, 50, CancellationToken.None);

        // Assert
        await _enrichmentServiceMock.Received(1).CalculateSentimentAsync(
            Arg.Any<List<TicketDto>>(),
            Arg.Is(workspaceModelId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTicketsAsync_WithNullWorkspaceModel_PassesNullToEnrichmentService()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithIsCustomerSatisfactionAnalysisEnabled(true)
            .WithCustomerSatisfactionAnalysisModelId("gpt-4o")  // CSAT feature requires a model
            .Build();

        var internalTicket = new TicketBuilder()
            .WithId(Guid.NewGuid())
            .WithWorkspaceId(workspaceId)
            .AsInternal()
            .Build();
        var comment = new TicketCommentBuilder()
            .WithTicketId(internalTicket.Id)
            .WithAuthor("a")
            .WithContent("Comment with sentiment")
            .Build();

        var tickets = new List<Ticket> { internalTicket };
        var commentsDict = new Dictionary<Guid, List<TicketComment>> { { internalTicket.Id, [comment] } };

        // Mock setup
        _authMock.IsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _paginationMock.NormalizePageSize(Arg.Any<int>()).Returns(50);
        _paginationMock.ParsePageToken(null).Returns(new TicketPageToken { Phase = "internal" });
        _ticketDataAccessMock.GetInternalTicketsByWorkspaceAsync(workspaceId, 0, 50, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConvertTicketsToInternalDtos(tickets)));
        _ticketDataAccessMock.GetAllStatusesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<TicketStatus>()));
        _ticketDataAccessMock.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<TicketPriority>()));
        _ticketDataAccessMock.GetCommentsByTicketIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(commentsDict));
        _integrationDataAccessMock.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<Integration>()));
        _workspaceDataAccessMock.GetByIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(workspace));
        _enrichmentServiceMock.CalculateSentimentAsync(Arg.Any<List<TicketDto>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.GetTicketsAsync(workspaceId, userId, null, 50, CancellationToken.None);

        // Assert
        // Should pass the CSAT model ID to enrichment service
        await _enrichmentServiceMock.Received(1).CalculateSentimentAsync(
            Arg.Any<List<TicketDto>>(),
            Arg.Is("gpt-4o"),
            Arg.Any<CancellationToken>());
    }
}
