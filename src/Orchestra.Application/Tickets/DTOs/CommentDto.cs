namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Represents a ticket comment from either internal database or external provider.
/// </summary>
/// <remarks>
/// For internal tickets, Id is the database GUID.
/// For external tickets, Id is the provider's comment identifier (e.g., Jira comment ID).
/// Author is display name extracted from JWT claims (internal) or provider user (external).
/// Timestamp is populated only for internal tickets using CreatedAt field; null for external tickets.
/// </remarks>
public record CommentDto(
    string Id,
    string Author,
    string Content,
    DateTime? Timestamp = null
);