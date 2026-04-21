using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class AIProviderConfigurationConfiguration : IEntityTypeConfiguration<AIProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<AIProviderConfiguration> builder)
    {
        builder.ToTable("AIProviderConfigurations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.WorkspaceId)
            .IsRequired();

        // Unique index — enforces one configuration per workspace at the DB level (AC-4)
        builder.HasIndex(c => c.WorkspaceId)
            .IsUnique();

        // ProviderType stored as its integer ordinal (0 = AzureOpenAI, 1 = Ollama)
        builder.Property(c => c.ProviderType)
            .IsRequired();

        // Endpoint: unified provider URL. Ciphertext for AzureOpenAI; plaintext for Ollama.
        builder.Property(c => c.Endpoint)
            .IsRequired(false)
            .HasMaxLength(2048);

        // ApiKey: ciphertext; max 4096 chars to hold AES-256-GCM encrypted blob (NFR: Security)
        builder.Property(c => c.ApiKey)
            .IsRequired(false)
            .HasMaxLength(4096);

        // DefaultModelId: default model for Ollama (required); optional null for AzureOpenAI
        builder.Property(c => c.DefaultModelId)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired(false);

        // FK: AIProviderConfigurations.WorkspaceId → Workspaces.Id with CASCADE delete.
        // Deleting a workspace removes its provider configuration automatically.
        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(c => c.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
