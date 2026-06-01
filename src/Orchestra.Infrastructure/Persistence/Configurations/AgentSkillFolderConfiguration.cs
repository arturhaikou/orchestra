using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class AgentSkillFolderConfiguration : IEntityTypeConfiguration<AgentSkillFolder>
{
    public void Configure(EntityTypeBuilder<AgentSkillFolder> builder)
    {
        builder.ToTable("AgentSkillFolders");

        builder.HasKey(asf => new { asf.AgentId, asf.SkillFolderId });

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(asf => asf.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SkillFolder>()
            .WithMany()
            .HasForeignKey(asf => asf.SkillFolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(asf => asf.SkillFolderId)
            .HasDatabaseName("IX_AgentSkillFolders_SkillFolderId");
    }
}
