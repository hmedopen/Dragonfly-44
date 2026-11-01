using Dragonfly44.Domain.Enums;

namespace Dragonfly44.Domain.Entities;

public class Chapter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public int ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public ChapterStatus Status { get; set; } = ChapterStatus.Draft;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Project? Project { get; set; }
    public List<Page> Pages { get; set; } = new();
    public ChapterContext? ChapterContext { get; set; }
}
