using Dragonfly44.Domain.Enums;

namespace Dragonfly44.Domain.Entities;

/// <summary>
/// Represents an immutable history entry for an AI translation request and its generated output.
/// Tracks source text, output text, alternatives, prompt version, provider, model, tokens, and cost.
/// </summary>
public class TranslationHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BubbleId { get; set; }
    public string SourceText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public string? AlternativeTranslationsJson { get; set; }
    public string? Notes { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public decimal? EstimatedCost { get; set; }
    public TranslationStatus Status { get; set; } = TranslationStatus.Completed;
    public bool IsAccepted { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAtUtc { get; set; }

    // Navigation property
    public Bubble? Bubble { get; set; }
}
