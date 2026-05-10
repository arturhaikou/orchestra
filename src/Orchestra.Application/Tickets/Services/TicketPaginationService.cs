using System.Text;
using System.Text.Json;
using Orchestra.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Orchestra.Application.Tickets.Services;

/// <summary>
/// Service implementation for ticket pagination.
/// Manages pagination state serialization and deserialization for stateless paging.
/// Extracted logic from TicketService.GetTicketsAsync (no behavioral changes).
/// </summary>
public class TicketPaginationService : ITicketPaginationService
{
    private readonly ILogger<TicketPaginationService> _logger;

    public TicketPaginationService(ILogger<TicketPaginationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a base64-encoded pagination token or returns default token.
    /// Extracted logic from TicketService.GetTicketsAsync token parsing (no changes).
    /// </summary>
    public TicketPageToken ParsePageToken(string? pageToken)
    {
        var currentToken = new TicketPageToken();

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            try
            {
                var tokenBytes = Convert.FromBase64String(pageToken);
                var tokenJson = Encoding.UTF8.GetString(tokenBytes);
                currentToken = JsonSerializer.Deserialize<TicketPageToken>(tokenJson) ?? new TicketPageToken();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid page token provided, starting from beginning");
                currentToken = new TicketPageToken();
            }
        }

        return currentToken;
    }

    /// <summary>
    /// Serializes pagination state to a base64-encoded token.
    /// Extracted logic from TicketService.GetTicketsAsync token generation (no changes).
    /// </summary>
    public string? SerializePageToken(TicketPageToken pageToken)
    {
        if (pageToken == null)
        {
            return null;
        }

        try
        {
            var tokenJson = JsonSerializer.Serialize(pageToken);
            var tokenBytes = Encoding.UTF8.GetBytes(tokenJson);
            return Convert.ToBase64String(tokenBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize page token");
            return null;
        }
    }

    /// <summary>
    /// Normalizes page size to enforce constraints (min 50, max 100).
    /// Extracted logic from TicketService.GetTicketsAsync page size validation (no changes).
    /// </summary>
    public int NormalizePageSize(int pageSize)
    {
        // Enforce max page size
        if (pageSize > 100)
            pageSize = 100;
        if (pageSize < 1)
            pageSize = 50;

        return pageSize;
    }
}
