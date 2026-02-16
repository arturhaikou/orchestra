using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class AgentToolActionConfiguration : IEntityTypeConfiguration<AgentToolAction>
{
    /// <summary>
    /// Configures the AgentToolAction entity as a join table with composite primary key.
    /// Both foreign keys have cascade delete behavior.
    /// </summary>
    /// <param name="builder">The entity type builder for AgentToolAction.</param>
    public void Configure(EntityTypeBuilder<AgentToolAction> builder)
    {
        builder.ToTable("agent_tool_actions");

        builder.HasKey(ata => new { ata.AgentId, ata.ToolActionId });

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(ata => ata.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ToolAction>()
            .WithMany()
            .HasForeignKey(ata => ata.ToolActionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}