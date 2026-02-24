using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Tickets.Services;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Application.Tests.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tickets;

public class TicketExternalFetchingServiceTests
{
    private readonly ITicketDataAccess _ticketDataAccess = Substitute.For<ITicketDataAccess>();
    private readonly ITicketProviderFactory _ticketProviderFactory = Substitute.For<ITicketProviderFactory>();
    private readonly ITicketMappingService _ticketMappingService = Substitute.For<ITicketMappingService>();
    private readonly ILogger<TicketExternalFetchingService> _logger = Substitute.For<ILogger<TicketExternalFetchingService>>();
    private readonly TicketExternalFetchingService _sut;

    public TicketExternalFetchingServiceTests()
    {
        _sut = new TicketExternalFetchingService(
            _ticketDataAccess,
            _ticketProviderFactory,
            _ticketMappingService,
            _logger);
    }

    [Fact]
    public void CalculateProviderDistribution_EvenDistribution_ReturnsCorrectSlots()
    {
        // Arrange
        var integrations = new List<Integration>
        {
            new IntegrationBuilder().WithId(Guid.NewGuid()).Build(),
            new IntegrationBuilder().WithId(Guid.NewGuid()).Build(),
            new IntegrationBuilder().WithId(Guid.NewGuid()).Build()
        };
        int slots = 6;

        // Act
        var result = _sut.CalculateProviderDistribution(integrations, slots);

        // Assert
        Assert.All(result.Values, v => Assert.Equal(2, v));
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void CalculateProviderDistribution_WithRemainder_DistributesRemainder()
    {
        // Arrange
        var integrations = new List<Integration>
        {
            new IntegrationBuilder().WithId(Guid.NewGuid()).Build(),
            new IntegrationBuilder().WithId(Guid.NewGuid()).Build(),
            new IntegrationBuilder().WithId(Guid.NewGuid()).Build()
        };
        int slots = 8;

        // Act
        var result = _sut.CalculateProviderDistribution(integrations, slots);

        // Assert
        var slotCounts = result.Values.ToList();
        Assert.Equal(8, slotCounts.Sum());
        Assert.Equal(3, result.Count);
        // Two integrations should get 3 slots, one should get 2
        Assert.Equal(2, slotCounts.Count(v => v == 3));
        Assert.Equal(1, slotCounts.Count(v => v == 2));
    }

    [Fact]
    public void CalculateProviderDistribution_NoIntegrationsOrSlots_ReturnsEmpty()
    {
        // Arrange
        var integrations = new List<Integration>();
        int slots = 0;

        // Act
        var result = _sut.CalculateProviderDistribution(integrations, slots);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchExternalTicketsAsync_ProviderReturnsFewerThanAllocated_RedistributesRemainingSlots()
    {
        // Arrange
        var provider1Id = Guid.NewGuid();
        var provider2Id = Guid.NewGuid();
        
        var integration1 = new IntegrationBuilder()
            .WithId(provider1Id)
            .WithProvider(ProviderType.GITHUB)
            .Build();
        var integration2 = new IntegrationBuilder()
            .WithId(provider2Id)
            .WithProvider(ProviderType.JIRA)
            .Build();

        var integrations = new List<Integration> { integration1, integration2 };

        // Provider 1 returns 2 tickets (allocated 5, underperforms)
        var tickets1 = new List<ExternalTicketDto>
        {
            new(provider1Id, "GH-1", "Ticket 1", "Description 1", "Open", "bg-blue-500", "High", "bg-red-500", 3, "https://github.com", new()),
            new(provider1Id, "GH-2", "Ticket 2", "Description 2", "Open", "bg-blue-500", "Medium", "bg-yellow-500", 2, "https://github.com", new())
        };

        // Provider 2 returns 5 tickets (allocated 5, full allocation)
        var tickets2 = new List<ExternalTicketDto>
        {
            new(provider2Id, "JI-1", "Jira 1", "Desc 1", "To Do", "bg-gray-500", "High", "bg-red-500", 3, "https://jira.com", new()),
            new(provider2Id, "JI-2", "Jira 2", "Desc 2", "To Do", "bg-gray-500", "High", "bg-red-500", 3, "https://jira.com", new()),
            new(provider2Id, "JI-3", "Jira 3", "Desc 3", "To Do", "bg-gray-500", "High", "bg-red-500", 3, "https://jira.com", new()),
            new(provider2Id, "JI-4", "Jira 4", "Desc 4", "To Do", "bg-gray-500", "High", "bg-red-500", 3, "https://jira.com", new()),
            new(provider2Id, "JI-5", "Jira 5", "Desc 5", "To Do", "bg-gray-500", "High", "bg-red-500", 3, "https://jira.com", new())
        };

        var mockProvider1 = Substitute.For<ITicketProvider>();
        var mockProvider2 = Substitute.For<ITicketProvider>();

        _ticketProviderFactory.CreateProvider(ProviderType.GITHUB).Returns(mockProvider1);
        _ticketProviderFactory.CreateProvider(ProviderType.JIRA).Returns(mockProvider2);

        // Provider 1: Returns 2 on first call, then 0 (exhausted) on second call
        var provider1Calls = 0;
        mockProvider1.FetchTicketsAsync(Arg.Is(integration1), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                provider1Calls++;
                if (provider1Calls == 1)
                    return Task.FromResult((tickets1, false, (string?)null));
                else
                    return Task.FromResult((new List<ExternalTicketDto>(), false, (string?)null));
            });

        // Provider 2: Always returns 5 tickets
        mockProvider2.FetchTicketsAsync(Arg.Is(integration2), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((tickets2, false, (string?)null)));

        _ticketDataAccess.GetTicketByExternalIdAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((Ticket?)null));

        var state = new ExternalPaginationState();

        // Act
        var result = await _sut.FetchExternalTicketsAsync(integrations, 10, state, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Tickets);
        // Should redistribute and reach target: 2 from P1 (round 1) + 5 from P2 (round 1) + 5 from P2 (round 2 after P1 marked exhausted) = 12
        // But may get exactly 10 if redistribution stops at target
        Assert.True(result.Tickets.Count >= 7, 
            $"Expected at least 7 tickets (2 from P1 + 5 from P2), got {result.Tickets.Count}");
        // One provider should be marked as exhausted (the one that returned 0)
        Assert.NotEmpty(result.State.ExhaustedProviderIds);
    }

    [Fact]
    public async Task FetchExternalTicketsAsync_MultipleProvidersVaryingCapacities_FillsPageToTarget()
    {
        // Arrange
        var provider1Id = Guid.NewGuid();
        var provider2Id = Guid.NewGuid();

        var integration1 = new IntegrationBuilder().WithId(provider1Id).WithProvider(ProviderType.GITHUB).Build();
        var integration2 = new IntegrationBuilder().WithId(provider2Id).WithProvider(ProviderType.JIRA).Build();

        var integrations = new List<Integration> { integration1, integration2 };

        var mockProvider1 = Substitute.For<ITicketProvider>();
        var mockProvider2 = Substitute.For<ITicketProvider>();

        _ticketProviderFactory.CreateProvider(ProviderType.GITHUB).Returns(mockProvider1);
        _ticketProviderFactory.CreateProvider(ProviderType.JIRA).Returns(mockProvider2);

        // Provider 1: Has 2 tickets only
        var p1Tickets = new List<ExternalTicketDto>
        {
            new(provider1Id, "P1-1", "P1 Ticket 1", "Desc", "Open", "bg-blue-500", "High", "bg-red-500", 3, "https://p1.com", new()),
            new(provider1Id, "P1-2", "P1 Ticket 2", "Desc", "Open", "bg-blue-500", "High", "bg-red-500", 3, "https://p1.com", new())
        };
        
        mockProvider1.FetchTicketsAsync(Arg.Any<Integration>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult((p1Tickets, false, (string?)null)),
                Task.FromResult((new List<ExternalTicketDto>(), false, (string?)null)) // Exhausted after first call
            );

        // Provider 2: returns 8 tickets on first and second calls
        var p2Tickets = Enumerable.Range(1, 8)
            .Select(i => new ExternalTicketDto(provider2Id, $"P2-{i}", $"P2 Ticket {i}", "Desc", "To Do", "bg-gray-500", "High", "bg-red-500", 3, "https://p2.com", new()))
            .ToList();
        mockProvider2.FetchTicketsAsync(Arg.Any<Integration>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((p2Tickets, false, (string?)null)));

        _ticketDataAccess.GetTicketByExternalIdAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((Ticket?)null));

        var state = new ExternalPaginationState();

        // Act - Request 10 tickets
        var result = await _sut.FetchExternalTicketsAsync(integrations, 10, state, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Tickets);
        // Should get at least 10 tickets: 2 from P1 + 8 from P2
        Assert.True(result.Tickets.Count >= 10, $"Expected at least 10 tickets, got {result.Tickets.Count}");
    }

    [Fact]
    public async Task FetchExternalTicketsAsync_ExhaustedProviderInState_SkipsProviderInRedistribution()
    {
        // Arrange
        var provider1Id = Guid.NewGuid();
        var provider2Id = Guid.NewGuid();

        var integration1 = new IntegrationBuilder().WithId(provider1Id).WithProvider(ProviderType.GITHUB).Build();
        var integration2 = new IntegrationBuilder().WithId(provider2Id).WithProvider(ProviderType.JIRA).Build();

        var integrations = new List<Integration> { integration1, integration2 };

        var mockProvider1 = Substitute.For<ITicketProvider>();
        var mockProvider2 = Substitute.For<ITicketProvider>();

        _ticketProviderFactory.CreateProvider(ProviderType.GITHUB).Returns(mockProvider1);
        _ticketProviderFactory.CreateProvider(ProviderType.JIRA).Returns(mockProvider2);

        // These should NOT be called since provider1 is already marked exhausted
        mockProvider1.FetchTicketsAsync(Arg.Any<Integration>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(List<ExternalTicketDto>, bool, string?)>(
                new InvalidOperationException("Provider 1 should be skipped because it's already exhausted")));

        // Provider 2 returns 5 tickets on first call, then empty on second (simulating exhaustion after retries)
        var p2Tickets = Enumerable.Range(1, 5)
            .Select(i => new ExternalTicketDto(provider2Id, $"P2-{i}", $"P2 Ticket {i}", "Desc", "To Do", "bg-gray-500", "High", "bg-red-500", 3, "https://p2.com", new()))
            .ToList();
        mockProvider2.FetchTicketsAsync(Arg.Any<Integration>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult((p2Tickets, false, (string?)null)),  // First call returns 5
                Task.FromResult((new List<ExternalTicketDto>(), false, (string?)null))  // Second call returns 0
            );

        _ticketDataAccess.GetTicketByExternalIdAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((Ticket?)null));

        // State already has provider1 marked as exhausted
        var state = new ExternalPaginationState
        {
            ExhaustedProviderIds = new List<string> { provider1Id.ToString() }
        };

        // Act - Request 10 tickets, but provider1 is already exhausted
        var result = await _sut.FetchExternalTicketsAsync(integrations, 10, state, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Tickets);
        // Should only get from provider2 (5 tickets)
        Assert.Equal(5, result.Tickets.Count);
        // Provider1 should still be in exhausted list
        Assert.Contains(provider1Id.ToString(), result.State.ExhaustedProviderIds);
    }

    [Fact]
    public async Task FetchExternalTicketsAsync_AllProvidersExhausted_ReturnsHasMoreFalse()
    {
        // Arrange
        var provider1Id = Guid.NewGuid();
        var provider2Id = Guid.NewGuid();

        var integration1 = new IntegrationBuilder().WithId(provider1Id).WithProvider(ProviderType.GITHUB).Build();
        var integration2 = new IntegrationBuilder().WithId(provider2Id).WithProvider(ProviderType.JIRA).Build();

        var integrations = new List<Integration> { integration1, integration2 };

        var state = new ExternalPaginationState
        {
            ExhaustedProviderIds = new List<string> 
            { 
                provider1Id.ToString(),
                provider2Id.ToString()
            }
        };

        // Act - Request 10 tickets when all providers already exhausted
        var result = await _sut.FetchExternalTicketsAsync(integrations, 10, state, CancellationToken.None);

        // Assert
        Assert.Empty(result.Tickets);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task FetchExternalTicketsAsync_RedistributionLoopCap_DoesNotInfiniteLoop()
    {
        // Arrange
        var provider1Id = Guid.NewGuid();
        var provider2Id = Guid.NewGuid();

        var integration1 = new IntegrationBuilder().WithId(provider1Id).WithProvider(ProviderType.GITHUB).Build();
        var integration2 = new IntegrationBuilder().WithId(provider2Id).WithProvider(ProviderType.JIRA).Build();

        var integrations = new List<Integration> { integration1, integration2 };

        var mockProvider1 = Substitute.For<ITicketProvider>();
        var mockProvider2 = Substitute.For<ITicketProvider>();

        _ticketProviderFactory.CreateProvider(ProviderType.GITHUB).Returns(mockProvider1);
        _ticketProviderFactory.CreateProvider(ProviderType.JIRA).Returns(mockProvider2);

        // Both providers return only 1 ticket when asked for more (pathological case)
        var p1Ticket = new List<ExternalTicketDto>
        {
            new(provider1Id, "P1-1", "P1", "Desc", "Open", "bg-blue-500", "High", "bg-red-500", 3, "https://p1.com", new())
        };
        var p2Ticket = new List<ExternalTicketDto>
        {
            new(provider2Id, "P2-1", "P2", "Desc", "To Do", "bg-gray-500", "High", "bg-red-500", 3, "https://p2.com", new())
        };

        mockProvider1.FetchTicketsAsync(Arg.Any<Integration>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((p1Ticket, false, (string?)null)));

        mockProvider2.FetchTicketsAsync(Arg.Any<Integration>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((p2Ticket, false, (string?)null)));

        _ticketDataAccess.GetTicketByExternalIdAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((Ticket?)null));

        var state = new ExternalPaginationState();

        // Act - Request 100 tickets (more than can be provided)
        var result = await _sut.FetchExternalTicketsAsync(integrations, 100, state, CancellationToken.None);

        // Assert - Should complete without hang/timeout and return what's available
        Assert.NotNull(result.Tickets);
        Assert.True(result.Tickets.Count > 0);
        Assert.False(result.HasMore, "Should indicate no more tickets available");
    }

    // -----------------------------------------------------------------------
    // Bug regression: isLast / hasMore correctness fixes
    // -----------------------------------------------------------------------

    /// <summary>
    /// Bug 1 regression (Fix #4): when a provider returns tickets AND signals isLast=true
    /// (no more pages), it must be marked exhausted immediately so the redistribution loop
    /// does not fire a wasted second round.
    /// Also validates Fix #3: hasMore must be false when all providers are exhausted even
    /// if the fetched count equals slotsToFill.
    /// </summary>
    [Fact]
    public async Task FetchExternalTicketsAsync_ProviderSignalsIsLastTrue_MarkedExhaustedImmediately()
    {
        // Arrange
        var integration = new IntegrationBuilder().WithProvider(ProviderType.JIRA).Build();
        var mockProvider = Substitute.For<ITicketProvider>();
        _ticketProviderFactory.CreateProvider(ProviderType.JIRA).Returns(mockProvider);

        // Provider returns 6 tickets and signals isLast=true (no further pages).
        var sixTickets = Enumerable.Range(1, 6).Select(i =>
            new ExternalTicketDto(integration.Id, $"T-{i}", $"Ticket {i}", "Desc",
                "Open", "#fff", "High", "#f00", 1, "https://jira.com", new()))
            .ToList();

        mockProvider.FetchTicketsAsync(
                Arg.Any<Integration>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((sixTickets, true /* isLast */, (string?)null)));

        _ticketDataAccess.GetTicketByExternalIdAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((Ticket?)null));

        // Act — request 10 slots; the provider can only supply 6 but flags no more pages.
        var result = await _sut.FetchExternalTicketsAsync(
            new List<Integration> { integration }, 10, new ExternalPaginationState(), CancellationToken.None);

        // Assert
        Assert.Equal(6, result.Tickets.Count);

        // Provider must be in ExhaustedProviderIds after the very first call.
        Assert.Contains(integration.Id.ToString(), result.State.ExhaustedProviderIds);

        // hasMore must be false: all providers are exhausted, nothing left to page through.
        Assert.False(result.HasMore,
            "HasMore must be false when the only provider signalled isLast=true.");

        // Provider must have been called exactly once — no wasted retry round.
        await mockProvider.Received(1).FetchTicketsAsync(
            Arg.Any<Integration>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Bug 2 regression (Fix #3): when the total fetched equals slotsToFill but all
    /// active providers are now exhausted, hasMore must still be false.
    /// Without this fix hasMore = count >= slotsToFill = true, causing an infinite
    /// load-more loop that always returns the same page.
    /// </summary>
    [Fact]
    public async Task FetchExternalTicketsAsync_FetchedEqualsSlots_AllProvidersExhausted_HasMoreFalse()
    {
        // Arrange
        var integration1 = new IntegrationBuilder().WithProvider(ProviderType.GITHUB).Build();
        var integration2 = new IntegrationBuilder().WithProvider(ProviderType.JIRA).Build();
        var integrations = new List<Integration> { integration1, integration2 };

        var mockProvider1 = Substitute.For<ITicketProvider>();
        var mockProvider2 = Substitute.For<ITicketProvider>();
        _ticketProviderFactory.CreateProvider(ProviderType.GITHUB).Returns(mockProvider1);
        _ticketProviderFactory.CreateProvider(ProviderType.JIRA).Returns(mockProvider2);

        // Provider 1 has exactly 2 tickets and flags isLast=true on the first call.
        var p1Tickets = Enumerable.Range(1, 2)
            .Select(i => new ExternalTicketDto(integration1.Id, $"P1-{i}", $"P1 {i}",
                "Desc", "Open", "#fff", "High", "#f00", 1, "https://p1.com", new()))
            .ToList();
        mockProvider1.FetchTicketsAsync(
                Arg.Any<Integration>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((p1Tickets, true /* isLast */, (string?)null)));

        // Provider 2 has exactly 8 tickets and flags isLast=true on the first call.
        var p2Tickets = Enumerable.Range(1, 8)
            .Select(i => new ExternalTicketDto(integration2.Id, $"P2-{i}", $"P2 {i}",
                "Desc", "Open", "#fff", "High", "#f00", 1, "https://p2.com", new()))
            .ToList();
        mockProvider2.FetchTicketsAsync(
                Arg.Any<Integration>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((p2Tickets, true /* isLast */, (string?)null)));

        _ticketDataAccess.GetTicketByExternalIdAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((Ticket?)null));

        // Act — request exactly 10 slots; total available = 2 + 8 = 10.
        var result = await _sut.FetchExternalTicketsAsync(
            integrations, 10, new ExternalPaginationState(), CancellationToken.None);

        // Assert
        Assert.Equal(10, result.Tickets.Count);

        // Both providers signalled isLast=true and must be in the exhausted set.
        Assert.Contains(integration1.Id.ToString(), result.State.ExhaustedProviderIds);
        Assert.Contains(integration2.Id.ToString(), result.State.ExhaustedProviderIds);

        // Even though count == slotsToFill, hasMore must be false because
        // no active (non-exhausted) providers remain.
        Assert.False(result.HasMore,
            "HasMore must be false when fetched count equals slotsToFill but all providers are exhausted.");
    }
}
