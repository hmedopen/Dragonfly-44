using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dragonfly44.Infrastructure.Persistence.Configurations;

public class BubbleConfiguration : IEntityTypeConfiguration<Bubble>
{
    public void Configure(EntityTypeBuilder<Bubble> builder)
    {
        builder.ToTable("Bubbles");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.RawText)
            .HasMaxLength(4000);

        builder.Property(b => b.TranslatedText)
            .HasMaxLength(4000);

        builder.Property(b => b.ShapeType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(b => b.IsFlagged)
            .HasDefaultValue(false);

        builder.Property(b => b.LastManualTranslation)
            .HasMaxLength(4000);

        builder.Property(b => b.IsAiTranslated)
            .HasDefaultValue(false);

        builder.HasOne(b => b.Character)
            .WithMany(ch => ch.Bubbles)
            .HasForeignKey(b => b.CharacterId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(b => new { b.PageId, b.ReadingOrderIndex });
        builder.HasIndex(b => new { b.PageId, b.IsFlagged });
    }
}
