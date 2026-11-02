using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dragonfly44.Infrastructure.Persistence.Configurations;

public class DraftConfiguration : IEntityTypeConfiguration<Draft>
{
    public void Configure(EntityTypeBuilder<Draft> builder)
    {
        builder.ToTable("Drafts");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.Description)
            .HasMaxLength(2000);

        builder.HasMany(d => d.Pages)
            .WithOne(p => p.Draft)
            .HasForeignKey(p => p.DraftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.Name);
    }
}
