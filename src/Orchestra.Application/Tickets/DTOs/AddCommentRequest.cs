namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Request to add a comment to a ticket.
/// For internal tickets, the comment is stored in the database.
/// For external tickets, the comment is proxied to the provider API.
/// </summary>
/// <remarks>
/// The author is automatically determined from the authenticated user (userId)
/// and retrieved from the database.
/// </remarks>
public record AddCommentRequest(
    string Content
);