using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class AiCliIntegrationConfiguration : IEntityTypeConfiguration<AiCliIntegration>
{
    public void Configure(EntityTypeBuilder<AiCliIntegration> builder)
    {
        builder.ToTable("AiCliIntegrations");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.WorkspaceId)
            .IsRequired();

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Provider)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.EncryptedCredential)
            .IsRequired(false);

        builder.Property(a => a.UseLoggedInUser)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.WorkingDirectory)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.CliPath)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UpdatedAt);

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(a => a.WorkspaceId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.WorkspaceId, a.Name })
            .IsUnique()
            .HasDatabaseName("IX_AiCliIntegrations_WorkspaceId_Name");

        builder.HasIndex(a => a.WorkspaceId)
            .HasDatabaseName("IX_AiCliIntegrations_WorkspaceId");
    }
}
