using Orchestra.Application.Tickets.Common;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for parsing and validating ticket identifiers.
/// Supports both internal (GUID) and external (composite) ticket ID formats.
/// </summary>
public interface ITicketIdParsingService
{
    /// <summary>
    /// Parses a ticket ID and returns its type and components.
    /// </summary>
    /// <param name="ticketId">The ticket ID to parse (GUID or composite format).</param>
    /// <returns>Parse result containing ticket type and parsed components.</returns>
    /// <exception cref="ArgumentException">Thrown if the ticket ID format is invalid.</exception>
    TicketIdParseResult Parse(string ticketId);

    /// <summary>
    /// Checks if a ticket ID is in composite format (integrationId:externalTicketId).
    /// </summary>
    /// <param name="ticketId">The ticket ID to check.</param>
    /// <returns>True if composite format, false otherwise.</returns>
    bool IsCompositeFormat(string ticketId);

    /// <summary>
    /// Checks if a ticket ID is in GUID format.
    /// </summary>
    /// <param name="ticketId">The ticket ID to check.</param>
    /// <returns>True if valid GUID format, false otherwise.</returns>
    bool IsGuidFormat(string ticketId);

    /// <summary>
    /// Builds a composite ticket ID from an integration ID and an external ticket ID.
    /// </summary>
    /// <param name="integrationId">The integration GUID.</param>
    /// <param name="externalTicketId">The external ticket identifier.</param>
    /// <returns>Composite ID in the format '{integrationId}:{externalTicketId}'.</returns>
    string BuildCompositeId(Guid integrationId, string externalTicketId);
}
