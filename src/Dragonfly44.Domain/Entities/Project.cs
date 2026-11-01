namespace Dragonfly44.Domain.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "ja";
    public string TargetLanguage { get; set; } = "ar";
    public string? Description { get; set; }
    public string? RootDirectory { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public List<Chapter> Chapters { get; set; } = new();
    public List<Character> Characters { get; set; } = new();
    public List<GlossaryEntry> GlossaryEntries { get; set; } = new();
}
