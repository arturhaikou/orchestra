using System;

namespace Orchestra.Application.Common.Exceptions
{
    public class TicketNotFoundException : Exception
    {
        public string TicketId { get; }

        public TicketNotFoundException(string ticketId)
            : base($"Ticket with ID '{ticketId}' was not found.")
        {
            TicketId = ticketId;
        }

        public TicketNotFoundException(string ticketId, Exception innerException)
            : base($"Ticket with ID '{ticketId}' was not found.", innerException)
        {
            TicketId = ticketId;
        }
    }
}