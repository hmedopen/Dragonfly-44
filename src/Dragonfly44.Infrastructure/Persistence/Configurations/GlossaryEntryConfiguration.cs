using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dragonfly44.Infrastructure.Persistence.Configurations;

public class GlossaryEntryConfiguration : IEntityTypeConfiguration<GlossaryEntry>
{
    public void Configure(EntityTypeBuilder<GlossaryEntry> builder)
    {
        builder.ToTable("GlossaryEntries");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.SourceTerm)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(g => g.TargetTerm)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(g => g.Category)
            .HasMaxLength(64);

        builder.Property(g => g.ContextNotes)
            .HasMaxLength(2000);

        builder.HasIndex(g => new { g.ProjectId, g.SourceTerm });
    }
}
