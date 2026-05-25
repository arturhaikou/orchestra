using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class AgentConversationSnapshotConfiguration : IEntityTypeConfiguration<AgentConversationSnapshot>
{
    public void Configure(EntityTypeBuilder<AgentConversationSnapshot> builder)
    {
        builder.ToTable("AgentConversationSnapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SerializedSessionJson).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.JobId, s.AgentId })
            .HasDatabaseName("IX_AgentConversationSnapshots_JobId_AgentId")
            .IsUnique();
    }
}
