namespace Orchestra.Domain.Entities;

public class TicketStatus
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Color { get; init; }

    public TicketStatus() { } // EF Core constructor
}