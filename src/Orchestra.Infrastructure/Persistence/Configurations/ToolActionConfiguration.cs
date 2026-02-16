using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class ToolActionConfiguration : IEntityTypeConfiguration<ToolAction>
{
    public void Configure(EntityTypeBuilder<ToolAction> builder)
    {
        builder.ToTable("tool_actions");

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

        builder.HasOne<ToolCategory>()
            .WithMany()
            .HasForeignKey(ta => ta.ToolCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ta => new { ta.ToolCategoryId, ta.Name })
            .IsUnique();

        builder.HasIndex(ta => ta.ToolCategoryId);
    }
}