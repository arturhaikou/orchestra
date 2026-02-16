namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Paginated response for ticket listings.
/// Supports cursor-based pagination with opaque tokens.
/// </summary>
public record PaginatedTicketsResponse(
    List<TicketDto> Items,
    string? NextPageToken,
    bool IsLast,
    int TotalCount
);