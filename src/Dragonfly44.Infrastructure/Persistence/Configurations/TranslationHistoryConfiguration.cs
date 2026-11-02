using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dragonfly44.Infrastructure.Persistence.Configurations;

public class TranslationHistoryConfiguration : IEntityTypeConfiguration<TranslationHistoryEntry>
{
    public void Configure(EntityTypeBuilder<TranslationHistoryEntry> builder)
    {
        builder.ToTable("TranslationHistories");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.SourceText)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(t => t.TranslatedText)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(t => t.AlternativeTranslationsJson)
            .HasMaxLength(4000);

        builder.Property(t => t.Notes)
            .HasMaxLength(4000);

        builder.Property(t => t.Provider)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Model)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.PromptVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.EstimatedCost)
            .HasPrecision(18, 6);

        builder.HasOne(t => t.Bubble)
            .WithMany(b => b.TranslationHistory)
            .HasForeignKey(t => t.BubbleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.BubbleId);
        builder.HasIndex(t => new { t.BubbleId, t.CreatedAtUtc });
    }
}
