using System;

namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when an invalid operation is attempted on a ticket.
/// Examples: updating status/priority on external tickets, deleting external tickets.
/// </summary>
public class InvalidTicketOperationException : Exception
{
    public InvalidTicketOperationException(string message)
        : base(message)
    {
    }

    public InvalidTicketOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}