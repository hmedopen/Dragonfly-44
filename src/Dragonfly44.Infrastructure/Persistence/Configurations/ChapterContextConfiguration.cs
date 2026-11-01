using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dragonfly44.Infrastructure.Persistence.Configurations;

public class ChapterContextConfiguration : IEntityTypeConfiguration<ChapterContext>
{
    public void Configure(EntityTypeBuilder<ChapterContext> builder)
    {
        builder.ToTable("ChapterContexts");
        builder.HasKey(cx => cx.Id);

        builder.Property(cx => cx.Summary)
            .HasMaxLength(4000);

        builder.Property(cx => cx.ToneGuidelines)
            .HasMaxLength(2000);

        builder.Property(cx => cx.CulturalNotes)
            .HasMaxLength(4000);

        builder.Property(cx => cx.ConsistencyRules)
            .HasMaxLength(4000);

        builder.HasIndex(cx => cx.ChapterId).IsUnique();
    }
}
