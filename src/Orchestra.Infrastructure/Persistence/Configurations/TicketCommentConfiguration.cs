using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.ToTable("TicketComments");
        
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Id)
            .ValueGeneratedNever();
        
        builder.Property(c => c.TicketId)
            .IsRequired();
        
        builder.Property(c => c.Author)
            .IsRequired()
            .HasMaxLength(255);
        
        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(10000);
        
        builder.Property(c => c.CreatedAt)
            .IsRequired();
        
        // Foreign Key to Ticket with cascade delete
        builder.HasOne(c => c.Ticket)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes for query performance
        builder.HasIndex(c => c.TicketId);
        builder.HasIndex(c => c.CreatedAt);
    }
}