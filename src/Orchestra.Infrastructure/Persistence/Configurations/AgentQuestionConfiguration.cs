using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class AgentQuestionConfiguration : IEntityTypeConfiguration<AgentQuestion>
{
    public void Configure(EntityTypeBuilder<AgentQuestion> builder)
    {
        builder.ToTable("AgentQuestions");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.QuestionsJson).IsRequired();
        builder.Property(q => q.AnswersJson).IsRequired(false);
        builder.Property(q => q.Status).IsRequired();
        builder.Property(q => q.CreatedAt).IsRequired();
        builder.Property(q => q.AnsweredAt).IsRequired(false);

        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(q => q.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => q.JobId).HasDatabaseName("IX_AgentQuestions_JobId");
        builder.HasIndex(q => new { q.WorkspaceId, q.Status })
            .HasDatabaseName("IX_AgentQuestions_WorkspaceId_Status");
    }
}
