namespace Dragonfly44.Domain.Entities;

public class ChapterContext
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string ToneGuidelines { get; set; } = string.Empty;
    public string CulturalNotes { get; set; } = string.Empty;
    public string ConsistencyRules { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Chapter? Chapter { get; set; }
}
