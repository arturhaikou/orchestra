using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class WorkflowStepSystemToolConfiguration : IEntityTypeConfiguration<WorkflowStepSystemTool>
{
    public void Configure(EntityTypeBuilder<WorkflowStepSystemTool> builder)
    {
        builder.ToTable("WorkflowStepSystemTools");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.WorkflowStepId)
            .IsRequired();

        builder.HasOne<WorkflowStep>()
            .WithMany()
            .HasForeignKey(t => t.WorkflowStepId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(t => t.ToolIdentifier)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.WorkflowStepId)
            .HasDatabaseName("IX_WorkflowStepSystemTools_WorkflowStepId");
    }
}
