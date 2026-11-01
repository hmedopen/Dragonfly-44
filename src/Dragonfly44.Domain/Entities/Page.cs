using Dragonfly44.Domain.Enums;

namespace Dragonfly44.Domain.Entities;

public class Page
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public int PageNumber { get; set; }
    public string ImageRelativePath { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public PageStatus Status { get; set; } = PageStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Chapter? Chapter { get; set; }
    public List<Bubble> Bubbles { get; set; } = new();
}
