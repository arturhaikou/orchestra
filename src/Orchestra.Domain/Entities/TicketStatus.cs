using System.ComponentModel.DataAnnotations;

namespace Orchestra.Domain.Entities;

public class TicketStatus
{
    public Guid Id { get; init; }

    [MaxLength(50)]
    public string Name { get; init; }

    [MaxLength(100)]
    public string Color { get; init; }

    public TicketStatus() { } // EF Core constructor
}