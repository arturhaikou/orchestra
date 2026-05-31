using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinitions");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.WorkspaceId)
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(w => w.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(w => w.Description)
            .IsRequired(false)
            .HasMaxLength(2000);

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        builder.Property(w => w.UpdatedAt)
            .IsRequired(false);

        builder.HasIndex(w => w.WorkspaceId)
            .HasDatabaseName("IX_WorkflowDefinitions_WorkspaceId");
    }
}
