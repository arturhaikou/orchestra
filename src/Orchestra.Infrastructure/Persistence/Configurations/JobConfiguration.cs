using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.AgentName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(j => j.InitialPrompt)
            .IsRequired();

        builder.Property(j => j.FinalResponse);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(2048);

        builder.Property(j => j.TicketTitle)
            .HasMaxLength(512);

        builder.Property(j => j.Status)
            .HasConversion<int>();

        builder.Property(j => j.TriggerType)
            .HasConversion<int>();

        // Foreign Key to Workspace with Restrict Delete (not Cascade)
        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(j => j.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index: (WorkspaceId, CreatedAt DESC)
        builder.HasIndex(j => new { j.WorkspaceId, j.CreatedAt })
            .HasDatabaseName("IX_Jobs_WorkspaceId_CreatedAt")
            .IsDescending(false, true);

        // Partial index for running jobs: Status IN (0, 1) = (Pending, Running)
        builder.HasIndex(j => j.WorkspaceId)
            .HasFilter("\"Status\" IN (0, 1)")
            .HasDatabaseName("IX_Jobs_WorkspaceId_Running");
    }
}
