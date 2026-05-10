using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public sealed class AgentMcpToolConfiguration : IEntityTypeConfiguration<AgentMcpTool>
{
    public void Configure(EntityTypeBuilder<AgentMcpTool> builder)
    {
        builder.ToTable("AgentMcpTools");

        builder.HasKey(x => new { x.AgentId, x.McpServerId, x.ToolName });

        builder.Property(x => x.AgentId).IsRequired();
        builder.Property(x => x.McpServerId).IsRequired();
        builder.Property(x => x.ToolName).IsRequired().HasMaxLength(200);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(x => x.AgentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<McpServer>()
            .WithMany()
            .HasForeignKey(x => x.McpServerId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.AgentId)
            .HasDatabaseName("IX_AgentMcpTools_AgentId");

        builder.HasIndex(x => x.McpServerId)
            .HasDatabaseName("IX_AgentMcpTools_McpServerId");
    }
}
