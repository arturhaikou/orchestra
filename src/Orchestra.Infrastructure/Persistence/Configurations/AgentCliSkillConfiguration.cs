using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class AgentCliSkillConfiguration : IEntityTypeConfiguration<AgentCliSkill>
{
    public void Configure(EntityTypeBuilder<AgentCliSkill> builder)
    {
        builder.ToTable("AgentCliSkills");

        builder.HasKey(acs => new { acs.AgentId, acs.SkillName });

        builder.Property(acs => acs.SkillName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(acs => acs.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(acs => acs.AgentId)
            .HasDatabaseName("IX_AgentCliSkills_AgentId");
    }
}
