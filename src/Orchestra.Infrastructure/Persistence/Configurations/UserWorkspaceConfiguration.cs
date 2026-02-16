using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class UserWorkspaceConfiguration : IEntityTypeConfiguration<UserWorkspace>
{
    /// <summary>
    /// Configures the UserWorkspace entity with cascade delete behavior.
    /// When a User or Workspace is deleted, all associated UserWorkspace entries are automatically removed.
    /// </summary>
    /// <param name="builder">The entity type builder for UserWorkspace.</param>
    public void Configure(EntityTypeBuilder<UserWorkspace> builder)
    {
        builder.ToTable("UserWorkspaces");

        builder.HasKey(uw => new { uw.UserId, uw.WorkspaceId });

        builder.HasOne(uw => uw.User)
            .WithMany()
            .HasForeignKey(uw => uw.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(uw => uw.Workspace)
            .WithMany()
            .HasForeignKey(uw => uw.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(uw => uw.JoinedAt)
            .IsRequired();

        builder.HasIndex(uw => uw.UserId);

        builder.HasIndex(uw => uw.WorkspaceId);
    }
}