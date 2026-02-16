using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class TicketStatusConfiguration : IEntityTypeConfiguration<TicketStatus>
{
    public void Configure(EntityTypeBuilder<TicketStatus> builder)
    {
        builder.HasKey(ts => ts.Id);

        builder.Property(ts => ts.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ts => ts.Color)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(ts => ts.Name)
            .IsUnique();

        builder.HasData(
            new TicketStatus
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "New",
                Color = "bg-blue-500/20 text-blue-400"
            },
            new TicketStatus
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Name = "To Do",
                Color = "bg-purple-500/20 text-purple-400"
            },
            new TicketStatus
            {
                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                Name = "InProgress",
                Color = "bg-yellow-500/20 text-yellow-400"
            },
            new TicketStatus
            {
                Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                Name = "Completed",
                Color = "bg-emerald-500/20 text-emerald-400"
            }
        );
    }
}