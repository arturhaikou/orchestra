using System;

namespace Orchestra.Application.Common.Exceptions;

public class UnauthorizedTicketAccessException : Exception
{
    public UnauthorizedTicketAccessException(Guid userId, string ticketId) 
        : base($"User '{userId}' is not authorized to access ticket '{ticketId}'.")
    {
    }
}