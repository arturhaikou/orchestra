using System.Collections.Generic;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Orchestra.Application.Tickets.Services;

public class TicketExternalFetchingService : IExternalTicketFetchingService
{
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly ITicketProviderFactory _ticketProviderFactory;
    private readonly ITicketMappingService _ticketMappingService;
    private readonly ILogger<TicketExternalFetchingService> _logger;

    public TicketExternalFetchingService(
        ITicketDataAccess ticketDataAccess,
        ITicketProviderFactory ticketProviderFactory,
        ITicketMappingService ticketMappingService,
        ILogger<TicketExternalFetchingService> logger)
    {
        _ticketDataAccess = ticketDataAccess;
        _ticketProviderFactory = ticketProviderFactory;
        _ticketMappingService = ticketMappingService;
        _logger = logger;
    }

    public async Task<(List<TicketDto> Tickets, bool HasMore, ExternalPaginationState State)> FetchExternalTicketsAsync(
        List<Integration> trackerIntegrations,
        int slotsToFill,
        ExternalPaginationState? currentState,
        CancellationToken cancellationToken)
    {
        var state = currentState ?? new ExternalPaginationState();
        
        // Use redistribution logic to fill slots with redistribution across providers
        var (tickets, updatedState) = await FetchWithRedistributionAsync(
            trackerIntegrations,
            slotsToFill,
            state,
            cancellationToken);

        // hasMore is true only when we filled the requested slots AND there are still active
        // (non-exhausted) providers that could supply more tickets on a subsequent page.
        // Without the second condition a fully-exhausted result set (e.g. 6 of 10 slots filled,
        // all providers done) would incorrectly return hasMore=true and the client would loop
        // forever receiving the same results.
        var allProvidersExhausted = trackerIntegrations.All(
            i => updatedState.ExhaustedProviderIds.Contains(i.Id.ToString()));
        var hasMore = tickets.Count >= slotsToFill && !allProvidersExhausted;

        return (tickets, hasMore, updatedState);
    }

    public Dictionary<Guid, int> CalculateProviderDistribution(
        List<Integration> integrations,
        int remainingSlots)
    {
        var distribution = new Dictionary<Guid, int>();
        
        if (integrations.Count == 0 || remainingSlots <= 0)
        {
            return distribution;
        }

        // Calculate base slots per provider and remainder
        var baseSlots = remainingSlots / integrations.Count;
        var remainder = remainingSlots % integrations.Count;

        // Distribute base slots to all providers
        foreach (var integration in integrations)
        {
            distribution[integration.Id] = baseSlots;
        }

        // Distribute remainder slots to first N providers
        for (int i = 0; i < remainder; i++)
        {
            distribution[integrations[i].Id]++;
        }

        return distribution;
    }

    /// <summary>
    /// Fetches external tickets with intelligent redistribution of unfilled slots.
    /// Implements a loop (max 3 iterations) to fill remaining slots when providers return fewer tickets than allocated.
    /// Tracks exhausted providers to avoid re-querying them in subsequent rounds.
    /// </summary>
    private async Task<(List<TicketDto> Tickets, ExternalPaginationState UpdatedState)> FetchWithRedistributionAsync(
        List<Integration> trackerIntegrations,
        int targetSlots,
        ExternalPaginationState state,
        CancellationToken cancellationToken)
    {
        var allFetchedTickets = new List<TicketDto>();
        const int maxRedistributionRounds = 3;
        
        for (int round = 0; round < maxRedistributionRounds; round++)
        {
            var remainingSlots = targetSlots - allFetchedTickets.Count;
            
            if (remainingSlots <= 0)
            {
                _logger.LogDebug(
                    "Target slots {TargetSlots} reached with {TicketCount} tickets. Stopping redistribution.",
                    targetSlots, allFetchedTickets.Count);
                break;
            }

            // Filter integrations to exclude exhausted providers
            var activeIntegrations = trackerIntegrations
                .Where(i => !state.ExhaustedProviderIds.Contains(i.Id.ToString()))
                .ToList();

            if (!activeIntegrations.Any())
            {
                _logger.LogDebug("All providers exhausted. Stopping redistribution.");
                break;
            }

            _logger.LogDebug(
                "Redistribution round {Round}: Fetching {RemainingSlots} tickets from {ActiveProviderCount} active providers",
                round + 1, remainingSlots, activeIntegrations.Count);

            // Calculate distribution for active providers
            var distribution = CalculateProviderDistribution(activeIntegrations, remainingSlots);

            // Track providers that returned fewer tickets than allocated in this round
            var underperformingProviders = new Dictionary<Guid, int>(); // Maps provider ID to count returned

            // Fetch from each provider and track results
            foreach (var (integrationId, requestedCount) in distribution)
            {
                if (requestedCount <= 0) continue;

                var integration = activeIntegrations.FirstOrDefault(i => i.Id == integrationId);
                if (integration == null) continue;

                var roundTickets = await FetchTicketsFromProviderAsync(
                    integration,
                    requestedCount,
                    state,
                    cancellationToken);

                allFetchedTickets.AddRange(roundTickets);
                underperformingProviders[integrationId] = roundTickets.Count;

                _logger.LogDebug(
                    "Round {Round}: Fetched {ReturnedCount} of {RequestedCount} tickets from {Provider}",
                    round + 1, roundTickets.Count, requestedCount, integration.Provider);

                // Note: provider-level exhaustion (isLast flag) is already handled inside
                // FetchTicketsFromProviderAsync. Here we only need the count==0 guard as a
                // safety net for providers that did not update state themselves.
            }

            // If we've reached target or all remaining providers are exhausted, stop
            if (allFetchedTickets.Count >= targetSlots)
            {
                _logger.LogDebug("Target slots reached. Stopping redistribution.");
                break;
            }

            // Check if all active providers were exhausted (returned 0)
            if (underperformingProviders.All(kvp => kvp.Value == 0))
            {
                _logger.LogDebug("All active providers exhausted in this round. Stopping redistribution.");
                break;
            }
        }

        return (allFetchedTickets, state);
    }

    /// <summary>
    /// Fetches tickets from a single provider and converts them to TicketDtos.
    /// Handles materialization merging and status/priority overlays.
    /// </summary>
    private async Task<List<TicketDto>> FetchTicketsFromProviderAsync(
        Integration integration,
        int requestedCount,
        ExternalPaginationState state,
        CancellationToken cancellationToken)
    {
        var resultTickets = new List<TicketDto>();

        try
        {
            var provider = _ticketProviderFactory.CreateProvider(integration.Provider);
            if (provider == null)
            {
                _logger.LogWarning(
                    "No provider implementation found for {Provider} (Integration: {IntegrationId})",
                    integration.Provider,
                    integration.Id);
                return resultTickets;
            }

            // Get provider token from state
            var providerToken = state.ProviderTokens.GetValueOrDefault(integration.Id.ToString());

            // Fetch tickets from provider
            var (externalTickets, isLast, nextProviderToken) = await provider.FetchTicketsAsync(
                integration,
                state.TotalExternalFetched,
                requestedCount,
                providerToken,
                cancellationToken);

            // Update state with new provider token and total count
            state.ProviderTokens[integration.Id.ToString()] = nextProviderToken;
            state.TotalExternalFetched += externalTickets.Count;

            // Mark provider as exhausted as soon as it signals no more pages are available.
            // Relying solely on count==0 in the redistribution loop would require a wasted
            // extra round-trip to discover exhaustion.
            if (isLast || externalTickets.Count == 0)
            {
                state.ExhaustedProviderIds.Add(integration.Id.ToString());
                _logger.LogDebug(
                    "Marking provider {IntegrationId} as exhausted (isLast={IsLast}, count={Count})",
                    integration.Id, isLast, externalTickets.Count);
            }

            if (externalTickets.Count == 0)
            {
                return resultTickets;
            }

            // Build materialized ticket lookup
            var materializedTickets = new Dictionary<(Guid, string), Ticket>();
            foreach (var extTicket in externalTickets)
            {
                var materializedTicket = await _ticketDataAccess.GetTicketByExternalIdAsync(
                    integration.Id,
                    extTicket.ExternalTicketId,
                    cancellationToken);

                if (materializedTicket != null)
                {
                    materializedTickets[(integration.Id, extTicket.ExternalTicketId)] = materializedTicket;
                }
            }

            // Map external tickets to DTOs and merge with materialized data
            foreach (var extTicket in externalTickets)
            {
                var compositeId = $"{integration.Id}:{extTicket.ExternalTicketId}";
                
                // Check if materialized
                var hasMaterialized = materializedTickets.TryGetValue(
                    (integration.Id, extTicket.ExternalTicketId),
                    out var materializedTicket);

                // For materialized tickets with internal status/priority, use those instead of external
                TicketStatusDto? status;
                TicketPriorityDto? priority;

                if (hasMaterialized && materializedTicket!.StatusId.HasValue)
                {
                    // Use internal status for materialized tickets
                    var internalStatus = await _ticketDataAccess.GetStatusByIdAsync(
                        materializedTicket.StatusId.Value, 
                        cancellationToken);
                    status = internalStatus != null 
                        ? new TicketStatusDto(internalStatus.Id, internalStatus.Name, internalStatus.Color)
                        : (!string.IsNullOrEmpty(extTicket.StatusName)
                            ? new TicketStatusDto(Guid.Empty, extTicket.StatusName, extTicket.StatusColor ?? "bg-gray-500")
                            : null);
                }
                else
                {
                    // Use external status for non-materialized tickets
                    status = !string.IsNullOrEmpty(extTicket.StatusName)
                        ? new TicketStatusDto(Guid.Empty, extTicket.StatusName, extTicket.StatusColor ?? "bg-gray-500")
                        : null;
                }

                if (hasMaterialized && materializedTicket!.PriorityId.HasValue)
                {
                    // Use internal priority for materialized tickets
                    var internalPriority = await _ticketDataAccess.GetPriorityByIdAsync(
                        materializedTicket.PriorityId.Value, 
                        cancellationToken);
                    priority = internalPriority != null 
                        ? new TicketPriorityDto(internalPriority.Id, internalPriority.Name, internalPriority.Color, internalPriority.Value)
                        : (!string.IsNullOrEmpty(extTicket.PriorityName)
                            ? new TicketPriorityDto(Guid.Empty, extTicket.PriorityName, extTicket.PriorityColor ?? "bg-gray-500", extTicket.PriorityValue)
                            : null);
                }
                else
                {
                    // Use external priority for non-materialized tickets
                    priority = !string.IsNullOrEmpty(extTicket.PriorityName)
                        ? new TicketPriorityDto(Guid.Empty, extTicket.PriorityName, extTicket.PriorityColor ?? "bg-gray-500", extTicket.PriorityValue)
                        : null;
                }

                var ticketDto = new TicketDto(
                    Id: compositeId,
                    WorkspaceId: integration.WorkspaceId,
                    Title: extTicket.Title,
                    Description: extTicket.Description,
                    Status: status,
                    Priority: priority,
                    Internal: false,
                    IntegrationId: integration.Id,
                    ExternalTicketId: extTicket.ExternalTicketId,
                    ExternalUrl: extTicket.ExternalUrl,
                    Source: integration.Provider.ToString().ToUpperInvariant(),
                    AssignedAgentId: materializedTicket?.AssignedAgentId,
                    AssignedWorkflowId: materializedTicket?.AssignedWorkflowId,
                    Comments: extTicket.Comments,
                    Satisfaction: null,
                    Summary: null
                );

                resultTickets.Add(ticketDto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to fetch tickets from {Provider} integration {IntegrationId}",
                integration.Provider,
                integration.Id);
        }

        return resultTickets;
    }
}
