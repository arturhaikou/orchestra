namespace Orchestra.Domain.Entities;

/// <summary>
/// Represents a comment on an internal ticket.
/// External ticket comments are fetched from provider APIs and not stored in the database.
/// </summary>
public class TicketComment
{
    /// <summary>
    /// Gets the unique identifier for this comment.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the identifier of the ticket this comment belongs to.
    /// </summary>
    public Guid TicketId { get; private set; }

    /// <summary>
    /// Gets the name of the user who authored this comment.
    /// </summary>
    public string Author { get; private set; }

    /// <summary>
    /// Gets the text content of the comment.
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when this comment was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    private TicketComment() { } // EF Core constructor

    /// <summary>
    /// Creates a new ticket comment with the specified parameters.
    /// </summary>
    /// <param name="ticketId">The identifier of the ticket to add the comment to.</param>
    /// <param name="author">The name of the comment author.</param>
    /// <param name="content">The text content of the comment.</param>
    /// <returns>A new TicketComment instance with auto-generated ID and creation timestamp.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when ticketId is empty, author is null/empty/whitespace, or content is null/empty/whitespace.
    /// </exception>
    public static TicketComment Create(
        Guid ticketId,
        string author,
        string content)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("Ticket ID is required.", nameof(ticketId));
        
        if (string.IsNullOrWhiteSpace(author))
            throw new ArgumentException("Author is required.", nameof(author));
        
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required.", nameof(content));

        return new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Author = author,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets the ticket that this comment belongs to.
    /// </summary>
    public Ticket Ticket { get; private set; } = null!;
}