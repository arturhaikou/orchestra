using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class TicketPriorityConfiguration : IEntityTypeConfiguration<TicketPriority>
{
    public void Configure(EntityTypeBuilder<TicketPriority> builder)
    {
        builder.HasKey(tp => tp.Id);

        builder.Property(tp => tp.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(tp => tp.Color)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(tp => tp.Value)
            .IsRequired();

        builder.HasIndex(tp => tp.Name)
            .IsUnique();

        builder.HasData(
            new TicketPriority
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Low",
                Color = "bg-slate-500/10 text-slate-400 border border-slate-500/20",
                Value = 1
            },
            new TicketPriority
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Medium",
                Color = "bg-blue-500/10 text-blue-400 border border-blue-500/20",
                Value = 2
            },
            new TicketPriority
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "High",
                Color = "bg-orange-500/10 text-orange-400 border border-orange-500/20",
                Value = 3
            },
            new TicketPriority
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Critical",
                Color = "bg-red-500/10 text-red-400 border border-red-500/20",
                Value = 4
            }
        );
    }
}