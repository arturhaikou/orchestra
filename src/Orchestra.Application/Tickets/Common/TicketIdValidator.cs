namespace Orchestra.Application.Tickets.Common;

/// <summary>
/// Validates and parses ticket identifiers.
/// Supports two formats:
/// - Internal: GUID format (e.g., "3fa85f64-5717-4562-b3fc-2c963f66afa6")
/// - External: Composite format (e.g., "{integrationId}:{externalTicketId}")
/// </summary>
public static class TicketIdValidator
{
    /// <summary>
    /// Parses a ticket ID and returns its type and components.
    /// </summary>
    /// <param name="ticketId">The ticket ID to parse.</param>
    /// <returns>A tuple containing the ticket ID type and parsed components.</returns>
    /// <exception cref="ArgumentException">Thrown if the ticket ID format is invalid.</exception>
    public static TicketIdParseResult Parse(string ticketId)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            throw new ArgumentException("Ticket ID cannot be empty.", nameof(ticketId));
        }

        // Check if composite format (contains ':')
        if (ticketId.Contains(':'))
        {
            return ParseCompositeId(ticketId);
        }
        else
        {
            return ParseInternalId(ticketId);
        }
    }

    /// <summary>
    /// Checks if a ticket ID is in composite format.
    /// </summary>
    public static bool IsCompositeFormat(string ticketId)
    {
        return !string.IsNullOrWhiteSpace(ticketId) && ticketId.Contains(':');
    }

    /// <summary>
    /// Checks if a ticket ID is in GUID format.
    /// </summary>
    public static bool IsGuidFormat(string ticketId)
    {
        return !string.IsNullOrWhiteSpace(ticketId) 
            && !ticketId.Contains(':') 
            && Guid.TryParse(ticketId, out _);
    }

    private static TicketIdParseResult ParseInternalId(string ticketId)
    {
        if (!Guid.TryParse(ticketId, out var guid))
        {
            throw new ArgumentException(
                $"Invalid internal ticket ID format: '{ticketId}'. Expected a valid GUID.",
                nameof(ticketId));
        }

        return new TicketIdParseResult(
            Type: TicketIdType.Internal,
            InternalId: guid,
            IntegrationId: null,
            ExternalTicketId: null
        );
    }

    private static TicketIdParseResult ParseCompositeId(string ticketId)
    {
        // Split at first colon (limit to 2 parts)
        var parts = ticketId.Split(':', 2);
        
        if (parts.Length != 2)
        {
            throw new ArgumentException(
                $"Invalid composite ticket ID format: '{ticketId}'. Expected format: '{{integrationId}}:{{externalTicketId}}'",
                nameof(ticketId));
        }

        var integrationIdString = parts[0];
        var externalTicketId = parts[1];

        // Validate integration ID is a GUID
        if (!Guid.TryParse(integrationIdString, out var integrationId))
        {
            throw new ArgumentException(
                $"Invalid integration ID in composite ticket ID: '{integrationIdString}'. Expected a valid GUID.",
                nameof(ticketId));
        }

        // Validate external ticket ID is not empty
        if (string.IsNullOrWhiteSpace(externalTicketId))
        {
            throw new ArgumentException(
                "External ticket ID cannot be empty in composite ticket ID.",
                nameof(ticketId));
        }

        return new TicketIdParseResult(
            Type: TicketIdType.External,
            InternalId: null,
            IntegrationId: integrationId,
            ExternalTicketId: externalTicketId
        );
    }
}

/// <summary>
/// Represents the type of ticket identifier.
/// </summary>
public enum TicketIdType
{
    /// <summary>
    /// Internal ticket with GUID format.
    /// </summary>
    Internal,

    /// <summary>
    /// External ticket with composite format (integrationId:externalTicketId).
    /// </summary>
    External
}

/// <summary>
/// Result of parsing a ticket ID.
/// </summary>
/// <param name="Type">The type of ticket ID (Internal or External).</param>
/// <param name="InternalId">The parsed GUID for internal tickets (null for external).</param>
/// <param name="IntegrationId">The parsed integration ID for external tickets (null for internal).</param>
/// <param name="ExternalTicketId">The parsed external ticket ID for external tickets (null for internal).</param>
public record TicketIdParseResult(
    TicketIdType Type,
    Guid? InternalId,
    Guid? IntegrationId,
    string? ExternalTicketId
);
