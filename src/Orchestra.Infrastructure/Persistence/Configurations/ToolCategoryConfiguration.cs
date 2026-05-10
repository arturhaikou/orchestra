using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class ToolCategoryConfiguration : IEntityTypeConfiguration<ToolCategory>
{
    public void Configure(EntityTypeBuilder<ToolCategory> builder)
    {
        builder.ToTable("ToolCategories");

        builder.HasKey(tc => tc.Id);

        builder.Property(tc => tc.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(tc => tc.Description)
            .HasMaxLength(500);

        builder.Property(tc => tc.ProviderType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(tc => tc.ServiceClassName)
            .HasMaxLength(200);

        builder.Property(tc => tc.IntegrationId);

        builder.Property(tc => tc.CreatedAt)
            .IsRequired();

        builder.Property(tc => tc.UpdatedAt);

        builder.Property(tc => tc.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(tc => tc.IsActive)
            .HasDatabaseName("IX_ToolCategories_IsActive");

        builder.HasOne<Integration>()
            .WithMany()
            .HasForeignKey(tc => tc.IntegrationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tc => tc.Name)
            .IsUnique();

        builder.HasIndex(tc => tc.ProviderType);

        builder.HasIndex(tc => tc.IntegrationId)
            .HasDatabaseName("IX_ToolCategories_IntegrationId");

        builder.HasIndex(tc => tc.IntegrationId)
            .IsUnique()
            .HasFilter("\"IntegrationId\" IS NOT NULL")
            .HasDatabaseName("IX_ToolCategories_IntegrationId_Unique");

        builder.Property(tc => tc.McpServerId);

        builder.HasOne<McpServer>()
            .WithMany()
            .HasForeignKey(tc => tc.McpServerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(tc => tc.McpServerId)
            .HasDatabaseName("IX_ToolCategories_McpServerId");
    }
}