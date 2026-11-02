using Dragonfly44.Application.Common;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;

namespace Dragonfly44.Application.Services;

public interface IDialogueService
{
    Task<Result<IReadOnlyList<Bubble>>> GetDialogueByChapterAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Bubble>>> GetDialogueByPageAsync(Guid pageId, CancellationToken cancellationToken = default);
    Task<Result<Bubble>> CreateDialogueAsync(
        Guid pageId,
        string rawText,
        string translatedText,
        BubbleShapeType shapeType = BubbleShapeType.StandardSpeech,
        double x = 0.1,
        double y = 0.1,
        double width = 0.2,
        double height = 0.15,
        CancellationToken cancellationToken = default);
    Task<Result<Bubble>> UpdateDialogueTextAsync(Guid bubbleId, string rawText, string translatedText, CancellationToken cancellationToken = default);
    Task<Result<Bubble>> UpdateClassificationAsync(Guid bubbleId, BubbleShapeType shapeType, CancellationToken cancellationToken = default);
    Task<Result<Bubble>> ToggleFlaggedAsync(Guid bubbleId, CancellationToken cancellationToken = default);
    Task<Result> ReorderDialoguesAsync(Guid pageId, IReadOnlyList<Guid> orderedBubbleIds, CancellationToken cancellationToken = default);
    Task<Result> DeleteDialogueAsync(Guid bubbleId, CancellationToken cancellationToken = default);
}
