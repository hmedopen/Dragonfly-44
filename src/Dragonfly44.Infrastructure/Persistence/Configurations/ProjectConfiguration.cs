using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dragonfly44.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.SourceLanguage)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(p => p.TargetLanguage)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.RootDirectory)
            .HasMaxLength(1024);

        builder.HasMany(p => p.Chapters)
            .WithOne(c => c.Project)
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Characters)
            .WithOne(ch => ch.Project)
            .HasForeignKey(ch => ch.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.GlossaryEntries)
            .WithOne(g => g.Project)
            .HasForeignKey(g => g.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.Name);
    }
}
