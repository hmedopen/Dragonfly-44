using Dragonfly44.Domain.Enums;

namespace Dragonfly44.Domain.Entities;

public class DraftPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DraftId { get; set; }
    public int PageNumber { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ImageRelativePath { get; set; } = string.Empty;
    public string FileFormat { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public PageStatus Status { get; set; } = PageStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Draft? Draft { get; set; }
}
