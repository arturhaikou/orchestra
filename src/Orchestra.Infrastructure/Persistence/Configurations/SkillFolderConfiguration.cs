using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence.Configurations;

public class SkillFolderConfiguration : IEntityTypeConfiguration<SkillFolder>
{
    public void Configure(EntityTypeBuilder<SkillFolder> builder)
    {
        builder.ToTable("SkillFolders");

        builder.HasKey(sf => sf.Id);

        builder.Property(sf => sf.WorkspaceId)
            .IsRequired();

        builder.Property(sf => sf.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(sf => sf.FolderPath)
            .IsRequired();

        builder.Property(sf => sf.CreatedAt)
            .IsRequired();

        builder.Property(sf => sf.UpdatedAt);

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(sf => sf.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(sf => sf.WorkspaceId)
            .HasDatabaseName("IX_SkillFolders_WorkspaceId");
    }
}
