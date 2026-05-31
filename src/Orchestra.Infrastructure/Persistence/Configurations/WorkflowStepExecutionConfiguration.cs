using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class WorkflowStepExecutionConfiguration : IEntityTypeConfiguration<WorkflowStepExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowStepExecution> builder)
    {
        builder.ToTable("WorkflowStepExecutions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.WorkflowExecutionId)
            .IsRequired();

        builder.HasOne<WorkflowExecution>()
            .WithMany()
            .HasForeignKey(s => s.WorkflowExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.StepIndex)
            .IsRequired();

        builder.Property(s => s.JobId)
            .IsRequired(false);

        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.StartedAt)
            .IsRequired();

        builder.Property(s => s.CompletedAt)
            .IsRequired(false);

        builder.Property(s => s.Output)
            .IsRequired(false);

        builder.HasIndex(s => s.WorkflowExecutionId)
            .HasDatabaseName("IX_WorkflowStepExecutions_WorkflowExecutionId");

        builder.HasIndex(s => s.JobId)
            .HasDatabaseName("IX_WorkflowStepExecutions_JobId");
    }
}
