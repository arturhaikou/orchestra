using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class McpServerConfiguration : IEntityTypeConfiguration<McpServer>
{
    public void Configure(EntityTypeBuilder<McpServer> builder)
    {
        builder.ToTable("McpServers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.WorkspaceId)
            .IsRequired();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.TransportType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(s => s.EndpointUrl)
            .HasMaxLength(500);

        builder.Property(s => s.AuthType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.EncryptedApiKey)
            .HasMaxLength(4096);

        builder.Property(s => s.Command)
            .HasMaxLength(500);

        builder.Property(s => s.Arguments)
            .HasMaxLength(4000);

        builder.Property(s => s.EncryptedEnvironmentVariables)
            .HasMaxLength(8000);

        builder.Property(s => s.ConnectionStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(McpConnectionStatus.Unknown);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt);

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(s => s.WorkspaceId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.WorkspaceId, s.Name })
            .IsUnique()
            .HasDatabaseName("IX_McpServers_WorkspaceId_Name");

        builder.HasIndex(s => s.WorkspaceId)
            .HasDatabaseName("IX_McpServers_WorkspaceId");
    }
}
