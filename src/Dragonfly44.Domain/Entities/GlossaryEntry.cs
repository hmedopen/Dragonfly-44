namespace Dragonfly44.Domain.Entities;

public class GlossaryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string SourceTerm { get; set; } = string.Empty;
    public string TargetTerm { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string? ContextNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Project? Project { get; set; }
}
