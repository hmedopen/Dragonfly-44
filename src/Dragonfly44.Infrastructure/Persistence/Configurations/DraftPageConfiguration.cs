using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dragonfly44.Infrastructure.Persistence.Configurations;

public class DraftPageConfiguration : IEntityTypeConfiguration<DraftPage>
{
    public void Configure(EntityTypeBuilder<DraftPage> builder)
    {
        builder.ToTable("DraftPages");
        builder.HasKey(dp => dp.Id);

        builder.Property(dp => dp.OriginalFileName)
            .IsRequired()
            .HasMaxLength(512)
            .HasDefaultValue(string.Empty);

        builder.Property(dp => dp.ImageRelativePath)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(dp => dp.FileFormat)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(string.Empty);

        builder.Property(dp => dp.FileSizeBytes)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(dp => dp.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(dp => new { dp.DraftId, dp.PageNumber }).IsUnique();
    }
}
