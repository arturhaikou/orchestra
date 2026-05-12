using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class AgentSubAgentConfiguration : IEntityTypeConfiguration<AgentSubAgent>
{
    /// <summary>
    /// Configures the AgentSubAgent entity as a self-referencing join table with composite
    /// primary key. ParentAgentId cascades on delete; SubAgentId uses restrict to avoid
    /// EF Core's multiple-cascade-path conflict.
    /// </summary>
    /// <param name="builder">The entity type builder for AgentSubAgent.</param>
    public void Configure(EntityTypeBuilder<AgentSubAgent> builder)
    {
        builder.ToTable("AgentSubAgents");

        builder.HasKey(asa => new { asa.ParentAgentId, asa.SubAgentId });

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(asa => asa.ParentAgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(asa => asa.SubAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(asa => asa.SubAgentId)
            .HasDatabaseName("IX_AgentSubAgents_SubAgentId");
    }
}
