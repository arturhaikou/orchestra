using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class AgentSkillConfiguration : IEntityTypeConfiguration<AgentSkill>
{
    public void Configure(EntityTypeBuilder<AgentSkill> builder)
    {
        builder.ToTable("AgentSkills");

        builder.HasKey(ask => new { ask.AgentId, ask.SkillId });

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(ask => ask.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Skill>()
            .WithMany()
            .HasForeignKey(ask => ask.SkillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ask => ask.SkillId)
            .HasDatabaseName("IX_AgentSkills_SkillId");
    }
}
