using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class ToolCategoryConfiguration : IEntityTypeConfiguration<ToolCategory>
{
    public void Configure(EntityTypeBuilder<ToolCategory> builder)
    {
        builder.ToTable("tool_categories");

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
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(tc => tc.CreatedAt)
            .IsRequired();

        builder.Property(tc => tc.UpdatedAt);

        builder.HasIndex(tc => tc.Name)
            .IsUnique();

        builder.HasIndex(tc => tc.ProviderType);
    }
}