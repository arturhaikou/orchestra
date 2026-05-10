using Microsoft.Extensions.Caching.Memory;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Application.Tickets.Services;

/// <summary>
/// Implementation of ticket lookup caching service using in-memory cache.
/// Wraps ITicketDataAccess with a decorator pattern to cache status and priority lookups.
/// Uses 5-minute TTL for cached data to balance freshness and performance.
/// </summary>
public class TicketLookupCacheService : ITicketLookupCacheService
{
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly IMemoryCache _cache;

    private const string STATUS_CACHE_KEY = "ticket_statuses_all";
    private const string PRIORITY_CACHE_KEY = "ticket_priorities_all";
    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(5);

    public TicketLookupCacheService(ITicketDataAccess ticketDataAccess, IMemoryCache cache)
    {
        _ticketDataAccess = ticketDataAccess;
        _cache = cache;
    }

    /// <summary>
    /// Gets all ticket statuses with in-memory caching.
    /// First call fetches from database and caches result.
    /// Subsequent calls return cached data until TTL expires.
    /// </summary>
    public async Task<List<TicketStatus>> GetAllStatusesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(STATUS_CACHE_KEY, out List<TicketStatus>? cachedStatuses) && cachedStatuses != null)
        {
            return cachedStatuses;
        }

        // Not in cache, fetch from data access
        var statuses = await _ticketDataAccess.GetAllStatusesAsync(cancellationToken);

        // Cache the result
        _cache.Set(STATUS_CACHE_KEY, statuses, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CACHE_DURATION
        });

        return statuses;
    }

    /// <summary>
    /// Gets all ticket priorities with in-memory caching.
    /// First call fetches from database and caches result.
    /// Subsequent calls return cached data until TTL expires.
    /// </summary>
    public async Task<List<TicketPriority>> GetAllPrioritiesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(PRIORITY_CACHE_KEY, out List<TicketPriority>? cachedPriorities) && cachedPriorities != null)
        {
            return cachedPriorities;
        }

        // Not in cache, fetch from data access
        var priorities = await _ticketDataAccess.GetAllPrioritiesAsync(cancellationToken);

        // Cache the result
        _cache.Set(PRIORITY_CACHE_KEY, priorities, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CACHE_DURATION
        });

        return priorities;
    }

    /// <summary>
    /// Invalidates the status cache, forcing a refresh on the next call.
    /// </summary>
    public Task InvalidateStatusCacheAsync()
    {
        _cache.Remove(STATUS_CACHE_KEY);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Invalidates the priority cache, forcing a refresh on the next call.
    /// </summary>
    public Task InvalidatePriorityCacheAsync()
    {
        _cache.Remove(PRIORITY_CACHE_KEY);
        return Task.CompletedTask;
    }
}
