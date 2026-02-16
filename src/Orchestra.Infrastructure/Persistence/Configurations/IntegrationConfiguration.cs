using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("Integrations");
        
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.Id)
            .ValueGeneratedNever(); // Set via domain logic
        
        builder.Property(i => i.WorkspaceId)
            .IsRequired();
        
        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(i => i.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);
        
        builder.Property(i => i.Icon)
            .HasMaxLength(50);
        
        builder.Property(i => i.Provider)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(100);
        
        builder.Property(i => i.Url)
            .HasMaxLength(500);
        
        builder.Property(i => i.Username)
            .HasMaxLength(255);
        
        builder.Property(i => i.EncryptedApiKey)
            .HasMaxLength(1000); // Encrypted data is larger than plain text
        
        builder.Property(i => i.FilterQuery)
            .HasMaxLength(500);
        
        builder.Property(i => i.Vectorize)
            .IsRequired()
            .HasDefaultValue(false);
        
        builder.Property(i => i.Connected)
            .IsRequired()
            .HasDefaultValue(false);
        
        builder.Property(i => i.LastSyncAt);
        
        builder.Property(i => i.CreatedAt)
            .IsRequired();
        
        builder.Property(i => i.UpdatedAt);
        
        builder.Property(i => i.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
        
        // Indexes
        builder.HasIndex(i => i.WorkspaceId)
            .HasDatabaseName("IX_Integrations_WorkspaceId");
        
        builder.HasIndex(i => i.Type)
            .HasDatabaseName("IX_Integrations_Type");
        
        builder.HasIndex(i => i.IsActive)
            .HasDatabaseName("IX_Integrations_IsActive");
        
        builder.HasIndex(i => new { i.WorkspaceId, i.Name })
            .HasDatabaseName("IX_Integrations_WorkspaceId_Name");
        
        // Foreign key relationship
        builder.HasOne(i => i.Workspace)
            .WithMany()
            .HasForeignKey(i => i.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade); // Delete integrations when workspace is deleted
    }
}
