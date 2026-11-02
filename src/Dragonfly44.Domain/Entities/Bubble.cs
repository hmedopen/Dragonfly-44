using Dragonfly44.Domain.Enums;

namespace Dragonfly44.Domain.Entities;

public class Bubble
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageId { get; set; }
    public Guid? CharacterId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public int ReadingOrderIndex { get; set; }
    public double Confidence { get; set; } = 1.0;
    public BubbleShapeType ShapeType { get; set; } = BubbleShapeType.StandardSpeech;
    public bool IsFlagged { get; set; } = false;
    public string? LastManualTranslation { get; set; }
    public bool IsAiTranslated { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Page? Page { get; set; }
    public Character? Character { get; set; }
    public ICollection<TranslationHistoryEntry> TranslationHistory { get; set; } = new List<TranslationHistoryEntry>();
}
