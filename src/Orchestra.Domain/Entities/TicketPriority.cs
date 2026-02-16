namespace Orchestra.Domain.Entities;

public class TicketPriority
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Color { get; init; }
    public int Value { get; init; }

    public TicketPriority() { } // EF Core constructor
}