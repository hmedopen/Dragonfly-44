using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dragonfly44.Infrastructure.Persistence;

public class Dragonfly44DbContext : DbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<Bubble> Bubbles => Set<Bubble>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<GlossaryEntry> GlossaryEntries => Set<GlossaryEntry>();
    public DbSet<ChapterContext> ChapterContexts => Set<ChapterContext>();

    public Dragonfly44DbContext(DbContextOptions<Dragonfly44DbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Dragonfly44DbContext).Assembly);
    }
}
