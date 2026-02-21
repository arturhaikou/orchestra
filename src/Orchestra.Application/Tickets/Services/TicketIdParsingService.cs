using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.Common;

namespace Orchestra.Application.Tickets.Services;

/// <summary>
/// Service implementation for parsing and validating ticket identifiers.
/// Wraps the static TicketIdValidator utility class to provide a testable, injectable service.
/// </summary>
public class TicketIdParsingService : ITicketIdParsingService
{
    /// <summary>
    /// Parses a ticket ID and returns its type and components.
    /// Delegates to the static TicketIdValidator.
    /// </summary>
    /// <param name="ticketId">The ticket ID to parse (GUID or composite format).</param>
    /// <returns>Parse result containing ticket type and parsed components.</returns>
    /// <exception cref="ArgumentException">Thrown if the ticket ID format is invalid.</exception>
    public TicketIdParseResult Parse(string ticketId)
    {
        return TicketIdValidator.Parse(ticketId);
    }

    /// <summary>
    /// Checks if a ticket ID is in composite format (integrationId:externalTicketId).
    /// Delegates to the static TicketIdValidator.
    /// </summary>
    /// <param name="ticketId">The ticket ID to check.</param>
    /// <returns>True if composite format, false otherwise.</returns>
    public bool IsCompositeFormat(string ticketId)
    {
        return TicketIdValidator.IsCompositeFormat(ticketId);
    }

    /// <summary>
    /// Checks if a ticket ID is in GUID format.
    /// Delegates to the static TicketIdValidator.
    /// </summary>
    /// <param name="ticketId">The ticket ID to check.</param>
    /// <returns>True if valid GUID format, false otherwise.</returns>
    public bool IsGuidFormat(string ticketId)
    {
        return TicketIdValidator.IsGuidFormat(ticketId);
    }

    /// <summary>
    /// Builds a composite ticket ID from an integration ID and an external ticket ID.
    /// </summary>
    public string BuildCompositeId(Guid integrationId, string externalTicketId)
    {
        return $"{integrationId}:{externalTicketId}";
    }
}
