using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.ToTable("WorkflowSteps");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.WorkflowDefinitionId)
            .IsRequired();

        builder.HasOne<WorkflowDefinition>()
            .WithMany()
            .HasForeignKey(s => s.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.Order)
            .IsRequired();

        builder.Property(s => s.AgentId)
            .IsRequired();

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.InstructionOverride)
            .IsRequired(false);

        builder.Property(s => s.PassPreviousOutput)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(s => s.WorkflowDefinitionId)
            .HasDatabaseName("IX_WorkflowSteps_WorkflowDefinitionId");
    }
}
