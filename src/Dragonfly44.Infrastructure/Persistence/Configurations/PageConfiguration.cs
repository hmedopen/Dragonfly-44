using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dragonfly44.Infrastructure.Persistence.Configurations;

public class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("Pages");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ImageRelativePath)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(p => p.OriginalFileName)
            .IsRequired()
            .HasMaxLength(512)
            .HasDefaultValue(string.Empty);

        builder.Property(p => p.FileFormat)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(string.Empty);

        builder.Property(p => p.FileSizeBytes)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(p => p.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasMany(p => p.Bubbles)
            .WithOne(b => b.Page)
            .HasForeignKey(b => b.PageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.ChapterId, p.PageNumber }).IsUnique();
    }
}
