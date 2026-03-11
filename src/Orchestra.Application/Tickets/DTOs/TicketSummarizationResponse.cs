namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Response wrapper for the summarization endpoint.
/// When <c>FeatureDisabled</c> is <c>true</c>, the <c>Ticket</c> field is null
/// and <c>Message</c> contains the user-friendly explanation.
/// When <c>FeatureDisabled</c> is <c>false</c>, <c>Ticket</c> contains the full TicketDto
/// (with the Summary field populated) and <c>Message</c> is null.
/// </summary>
public record TicketSummarizationResponse(
    TicketDto? Ticket,
    bool FeatureDisabled,
    string? Message);
