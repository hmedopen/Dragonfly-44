using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dragonfly44.Infrastructure.Persistence.Configurations;

public class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        builder.ToTable("Chapters");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(c => c.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasMany(c => c.Pages)
            .WithOne(p => p.Chapter)
            .HasForeignKey(p => p.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.ChapterContext)
            .WithOne(cx => cx.Chapter)
            .HasForeignKey<ChapterContext>(cx => cx.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.ProjectId, c.ChapterNumber }).IsUnique();
    }
}
