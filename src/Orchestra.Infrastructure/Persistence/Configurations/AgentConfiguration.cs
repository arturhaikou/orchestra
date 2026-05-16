using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.WorkspaceId)
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(a => a.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Role)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(a => a.AvatarUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.CustomInstructions)
            .IsRequired(false);

        builder.Property(a => a.ProjectPrinciples)
            .IsRequired(false);

        builder.Property(a => a.Capabilities)
            .HasColumnType("jsonb");

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UpdatedAt);

        builder.Property(a => a.Model)
            .HasMaxLength(500);

        builder.Property(a => a.TemplateIdentifier)
            .IsRequired(false)
            .HasMaxLength(200);

        builder.Property(a => a.TemplateVersion)
            .IsRequired(false);

        builder.Property(a => a.AiCliIntegrationId)
            .IsRequired(false);

        builder.HasOne<AiCliIntegration>()
            .WithMany()
            .HasForeignKey(a => a.AiCliIntegrationId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(a => new { a.WorkspaceId, a.TemplateIdentifier })
            .IsUnique()
            .HasFilter("\"TemplateIdentifier\" IS NOT NULL")
            .HasDatabaseName("IX_Agents_WorkspaceId_TemplateIdentifier");

        builder.HasIndex(a => a.WorkspaceId);
    }
}