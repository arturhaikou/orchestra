using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.Description)
            .HasMaxLength(10000);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt);

        builder.Property(t => t.IntegrationId)
            .IsRequired(false);

        builder.Property(t => t.ExternalTicketId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(t => t.AssignedAgentId)
            .IsRequired(false);

        builder.Property(t => t.AssignedWorkflowId)
            .IsRequired(false);

        // Foreign Key to Workspace with Cascade Delete
        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(t => t.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign Key to TicketStatus (optional for external tickets)
        builder.HasOne<TicketStatus>()
            .WithMany()
            .HasForeignKey(t => t.StatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Foreign Key to TicketPriority (optional for external tickets)
        builder.HasOne<TicketPriority>()
            .WithMany()
            .HasForeignKey(t => t.PriorityId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Foreign Key to Integration with cascade delete — no navigation property on Ticket
        builder.HasOne<Integration>()
            .WithMany()
            .HasForeignKey(t => t.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        // Unique composite index with filter (prevents duplicate materializations)
        builder.HasIndex(t => new { t.IntegrationId, t.ExternalTicketId })
            .IsUnique()
            .HasFilter("\"IntegrationId\" IS NOT NULL AND \"ExternalTicketId\" IS NOT NULL");

        // Comments relationship — no navigation properties on Ticket or TicketComment
        builder.HasMany<TicketComment>()
            .WithOne()
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.WorkspaceId);

        // Composite index for efficient filtering on WorkspaceId + IsInternal (Audit finding B-1)
        builder.HasIndex(t => new { t.WorkspaceId, t.IsInternal })
            .HasDatabaseName("IX_Tickets_WorkspaceId_IsInternal");

        // Partial composite index for agent execution polling (Audit finding B-2)
        builder.HasIndex(t => new { t.AssignedAgentId, t.StatusId })
            .HasFilter("\"AssignedAgentId\" IS NOT NULL")
            .HasDatabaseName("IX_Tickets_AssignedAgentId_StatusId_Partial");

        // Single-column index on StatusId for join/filter efficiency (Audit finding B-3)
        builder.HasIndex(t => t.StatusId)
            .HasDatabaseName("IX_Tickets_StatusId");

        // Single-column index on PriorityId for join/filter efficiency (Audit finding B-3)
        builder.HasIndex(t => t.PriorityId)
            .HasDatabaseName("IX_Tickets_PriorityId");

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(t => t.AssignedAgentId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}