using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class ToolActionConfiguration : IEntityTypeConfiguration<ToolAction>
{
    public void Configure(EntityTypeBuilder<ToolAction> builder)
    {
        builder.ToTable("ToolActions");

        builder.HasKey(ta => ta.Id);

        builder.Property(ta => ta.ToolCategoryId)
            .IsRequired();

        builder.Property(ta => ta.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ta => ta.Description)
            .HasMaxLength(500);

        builder.Property(ta => ta.MethodName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ta => ta.DangerLevel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(ta => ta.IsMcpTool)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(ta => ta.McpToolSchema)
            .HasColumnType("jsonb");

        builder.Property(ta => ta.IntegrationId);

        builder.Property(ta => ta.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(ta => ta.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(ta => ta.LastSyncedAt)
            .IsRequired(false);

        builder.HasOne<ToolCategory>()
            .WithMany()
            .HasForeignKey(ta => ta.ToolCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Integration>()
            .WithMany()
            .HasForeignKey(ta => ta.IntegrationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(ta => new { ta.ToolCategoryId, ta.Name })
            .IsUnique();

        builder.HasIndex(ta => ta.ToolCategoryId);

        builder.HasIndex(ta => new { ta.ToolCategoryId, ta.MethodName })
            .IsUnique()
            .HasDatabaseName("IX_ToolActions_ToolCategoryId_MethodName");

        builder.HasIndex(ta => ta.IsEnabled)
            .HasDatabaseName("IX_ToolActions_IsEnabled");

        builder.HasIndex(ta => ta.IsActive)
            .HasDatabaseName("IX_ToolActions_IsActive");

        builder.HasIndex(ta => new { ta.IntegrationId, ta.IsActive })
            .HasDatabaseName("IX_ToolActions_IntegrationId_IsActive");
    }
}