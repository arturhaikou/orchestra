using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class IntegrationConfiguration(bool isInMemory = false) : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("Integrations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedNever();

        builder.Property(i => i.WorkspaceId)
            .IsRequired();

        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(100);

        ConfigureTypesProperty(builder);

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
            .HasMaxLength(4096);

        builder.Property(i => i.FilterQuery)
            .HasMaxLength(500);

        builder.Property(i => i.Vectorize)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(i => i.LastSyncAt);

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.Property(i => i.UpdatedAt);

        builder.Property(i => i.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(i => i.WorkspaceId)
            .HasDatabaseName("IX_Integrations_WorkspaceId");

        builder.HasIndex(i => i.IsActive)
            .HasDatabaseName("IX_Integrations_IsActive");

        builder.HasIndex(i => new { i.WorkspaceId, i.Name })
            .HasDatabaseName("IX_Integrations_WorkspaceId_Name");

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(i => i.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureTypesProperty(EntityTypeBuilder<Integration> builder)
    {
        if (isInMemory)
            ConfigureTypesForInMemory(builder);
        else
            ConfigureTypesForPostgres(builder);
    }

    private static void ConfigureTypesForInMemory(EntityTypeBuilder<Integration> builder)
    {
        builder.Property(i => i.Types)
            .IsRequired()
            .HasConversion(
                v => string.Join(',', v.Select(t => t.ToString())),
                v => v.Length == 0
                    ? new List<IntegrationType>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(Enum.Parse<IntegrationType>).ToList());
    }

    private static void ConfigureTypesForPostgres(EntityTypeBuilder<Integration> builder)
    {
        var typesComparer = new ValueComparer<List<IntegrationType>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (h, e) => HashCode.Combine(h, e.GetHashCode())),
            v => v.ToList());

        builder.Property(i => i.Types)
            .IsRequired()
            .HasConversion(
                v => v.Select(t => t.ToString()).ToArray(),
                v => v.Select(s => Enum.Parse<IntegrationType>(s)).ToList(),
                typesComparer)
            .HasColumnType("text[]");
    }
}
