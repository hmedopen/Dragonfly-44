using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dragonfly44.Infrastructure.Persistence.Configurations;

public class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("Characters");
        builder.HasKey(ch => ch.Id);

        builder.Property(ch => ch.CanonicalName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(ch => ch.LocalizedName)
            .HasMaxLength(256);

        builder.Property(ch => ch.ColorHex)
            .HasMaxLength(16);

        builder.Property(ch => ch.Description)
            .HasMaxLength(2000);

        builder.Property(ch => ch.Notes)
            .HasMaxLength(4000);

        builder.HasIndex(ch => new { ch.ProjectId, ch.CanonicalName });
    }
}
