using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class JobStepConfiguration : IEntityTypeConfiguration<JobStep>
{
    public void Configure(EntityTypeBuilder<JobStep> builder)
    {
        builder.ToTable("JobSteps");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Content);

        builder.Property(s => s.ToolName)
            .HasMaxLength(256);

        builder.Property(s => s.StepType)
            .HasConversion<int>();

        // Foreign Key to Job with Cascade Delete
        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index: (JobId, Sequence ASC)
        builder.HasIndex(s => new { s.JobId, s.Sequence })
            .HasDatabaseName("IX_JobSteps_JobId_Sequence");

        builder.Property(s => s.AgentName)
            .HasMaxLength(256);

        // Index: (ParentStepId) for tree queries
        builder.HasIndex(s => s.ParentStepId)
            .HasDatabaseName("IX_JobSteps_ParentStepId");
    }
}
