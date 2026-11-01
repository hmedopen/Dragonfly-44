namespace Dragonfly44.Domain.Entities;

public class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string CanonicalName { get; set; } = string.Empty;
    public string LocalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ColorHex { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Project? Project { get; set; }
    public List<Bubble> Bubbles { get; set; } = new();
}
