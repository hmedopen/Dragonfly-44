namespace Dragonfly44.Domain.Enums;

/// <summary>
/// Status of an AI translation generation and review lifecycle.
/// </summary>
public enum TranslationStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Accepted = 3,
    Rejected = 4
}
