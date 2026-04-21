using System.ComponentModel.DataAnnotations;

namespace Orchestra.Domain.Entities;

public class TicketPriority
{
    public Guid Id { get; init; }
    
    [MaxLength(50)]
    public string Name { get; init; }
    
    [MaxLength(100)]
    public string Color { get; init; }
    
    public int Value { get; init; }

    public TicketPriority() { } // EF Core constructor
}