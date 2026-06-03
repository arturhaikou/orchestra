using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class WorkflowExecutionConfiguration : IEntityTypeConfiguration<WorkflowExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowExecution> builder)
    {
        builder.ToTable("WorkflowExecutions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.WorkflowDefinitionId)
            .IsRequired();

        builder.HasOne<WorkflowDefinition>()
            .WithMany()
            .HasForeignKey(e => e.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.TicketId)
            .IsRequired();

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.WorkspaceId)
            .IsRequired();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.CurrentStepIndex)
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .IsRequired();

        builder.Property(e => e.CompletedAt)
            .IsRequired(false);

        builder.HasIndex(e => e.TicketId)
            .HasDatabaseName("IX_WorkflowExecutions_TicketId");

        builder.HasIndex(e => e.WorkspaceId)
            .HasDatabaseName("IX_WorkflowExecutions_WorkspaceId");

        builder.Property(e => e.WorkflowJobId)
            .IsRequired(false);

        builder.Property(e => e.ActiveTicketId)
            .IsRequired(false);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(e => e.ActiveTicketId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
