using Dragonfly44.Application.Common;
using Dragonfly44.Application.Common.TypeR;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Infrastructure.Services;

public class DialogueService : IDialogueService
{
    private readonly IBubbleRepository _bubbleRepository;
    private readonly IPageRepository _pageRepository;
    private readonly ILogger<DialogueService> _logger;

    public DialogueService(
        IBubbleRepository bubbleRepository,
        IPageRepository pageRepository,
        ILogger<DialogueService> logger)
    {
        _bubbleRepository = bubbleRepository;
        _pageRepository = pageRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<Bubble>>> GetDialogueByChapterAsync(
        Guid chapterId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bubbles = await _bubbleRepository.GetByChapterIdAsync(chapterId, cancellationToken);
            return Result<IReadOnlyList<Bubble>>.Success(bubbles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch dialogues for chapter {ChapterId}", chapterId);
            return Result<IReadOnlyList<Bubble>>.Failure($"Failed to fetch dialogues: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<Bubble>>> GetDialogueByPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bubbles = await _bubbleRepository.GetByPageIdAsync(pageId, cancellationToken);
            return Result<IReadOnlyList<Bubble>>.Success(bubbles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch dialogues for page {PageId}", pageId);
            return Result<IReadOnlyList<Bubble>>.Failure($"Failed to fetch dialogues: {ex.Message}");
        }
    }

    public async Task<Result<Bubble>> CreateDialogueAsync(
        Guid pageId,
        string rawText,
        string translatedText,
        BubbleShapeType shapeType = BubbleShapeType.StandardSpeech,
        double x = 0.1,
        double y = 0.1,
        double width = 0.2,
        double height = 0.15,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
            if (page == null)
            {
                return Result<Bubble>.Failure($"Page with ID {pageId} was not found.");
            }

            var existing = await _bubbleRepository.GetByPageIdAsync(pageId, cancellationToken);
            var nextOrderIndex = existing.Count > 0 ? existing.Max(b => b.ReadingOrderIndex) + 1 : 1;

            // Handle TypeR prefix if embedded in text
            var finalShapeType = shapeType;
            var cleanRaw = rawText ?? string.Empty;
            var cleanTranslated = translatedText ?? string.Empty;

            var rawParse = TypeRPrefixHandler.ParseLine(cleanRaw);
            if (rawParse.HasPrefix)
            {
                finalShapeType = rawParse.ShapeType;
                cleanRaw = rawParse.CleanText;
            }

            var transParse = TypeRPrefixHandler.ParseLine(cleanTranslated);
            if (transParse.HasPrefix)
            {
                finalShapeType = transParse.ShapeType;
                cleanTranslated = transParse.CleanText;
            }

            var bubble = new Bubble
            {
                Id = Guid.NewGuid(),
                PageId = pageId,
                RawText = cleanRaw.Trim(),
                TranslatedText = cleanTranslated.Trim(),
                ShapeType = finalShapeType,
                ReadingOrderIndex = nextOrderIndex,
                Confidence = 1.0,
                X = Math.Clamp(x, 0.0, 1.0),
                Y = Math.Clamp(y, 0.0, 1.0),
                Width = Math.Clamp(width, 0.01, 1.0 - Math.Clamp(x, 0.0, 1.0)),
                Height = Math.Clamp(height, 0.01, 1.0 - Math.Clamp(y, 0.0, 1.0)),
                IsFlagged = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _bubbleRepository.AddAsync(bubble, cancellationToken);
            await _bubbleRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created dialogue {BubbleId} on page {PageId} with order {Index}",
                bubble.Id, pageId, bubble.ReadingOrderIndex);

            return Result<Bubble>.Success(bubble);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create dialogue on page {PageId}", pageId);
            return Result<Bubble>.Failure($"Failed to create dialogue: {ex.Message}");
        }
    }

    public async Task<Result<Bubble>> UpdateDialogueTextAsync(
        Guid bubbleId,
        string rawText,
        string translatedText,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bubble = await _bubbleRepository.GetByIdAsync(bubbleId, cancellationToken);
            if (bubble == null)
            {
                return Result<Bubble>.Failure($"Dialogue with ID {bubbleId} was not found.");
            }

            var cleanRaw = rawText ?? string.Empty;
            var cleanTranslated = translatedText ?? string.Empty;

            var rawParse = TypeRPrefixHandler.ParseLine(cleanRaw);
            if (rawParse.HasPrefix)
            {
                bubble.ShapeType = rawParse.ShapeType;
                cleanRaw = rawParse.CleanText;
            }

            var transParse = TypeRPrefixHandler.ParseLine(cleanTranslated);
            if (transParse.HasPrefix)
            {
                bubble.ShapeType = transParse.ShapeType;
                cleanTranslated = transParse.CleanText;
            }

            bubble.RawText = cleanRaw;
            bubble.TranslatedText = cleanTranslated;

            await _bubbleRepository.UpdateAsync(bubble, cancellationToken);
            await _bubbleRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated dialogue text for {BubbleId}", bubbleId);
            return Result<Bubble>.Success(bubble);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update dialogue text for {BubbleId}", bubbleId);
            return Result<Bubble>.Failure($"Failed to update dialogue text: {ex.Message}");
        }
    }

    public async Task<Result<Bubble>> UpdateClassificationAsync(
        Guid bubbleId,
        BubbleShapeType shapeType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bubble = await _bubbleRepository.GetByIdAsync(bubbleId, cancellationToken);
            if (bubble == null)
            {
                return Result<Bubble>.Failure($"Dialogue with ID {bubbleId} was not found.");
            }

            bubble.ShapeType = shapeType;
            await _bubbleRepository.UpdateAsync(bubble, cancellationToken);
            await _bubbleRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated classification for dialogue {BubbleId} to {ShapeType}", bubbleId, shapeType);
            return Result<Bubble>.Success(bubble);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update classification for {BubbleId}", bubbleId);
            return Result<Bubble>.Failure($"Failed to update classification: {ex.Message}");
        }
    }

    public async Task<Result<Bubble>> ToggleFlaggedAsync(
        Guid bubbleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bubble = await _bubbleRepository.GetByIdAsync(bubbleId, cancellationToken);
            if (bubble == null)
            {
                return Result<Bubble>.Failure($"Dialogue with ID {bubbleId} was not found.");
            }

            bubble.IsFlagged = !bubble.IsFlagged;
            await _bubbleRepository.UpdateAsync(bubble, cancellationToken);
            await _bubbleRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Toggled flagged state for dialogue {BubbleId} to {IsFlagged}", bubbleId, bubble.IsFlagged);
            return Result<Bubble>.Success(bubble);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle flag for {BubbleId}", bubbleId);
            return Result<Bubble>.Failure($"Failed to toggle flag: {ex.Message}");
        }
    }

    public async Task<Result> ReorderDialoguesAsync(
        Guid pageId,
        IReadOnlyList<Guid> orderedBubbleIds,
        CancellationToken cancellationToken = default)
    {
        if (orderedBubbleIds == null || orderedBubbleIds.Count == 0)
        {
            return Result.Success();
        }

        try
        {
            var pageBubbles = await _bubbleRepository.GetByPageIdAsync(pageId, cancellationToken);
            var bubbleMap = pageBubbles.ToDictionary(b => b.Id);

            int order = 1;
            var updated = new List<Bubble>();

            foreach (var id in orderedBubbleIds)
            {
                if (bubbleMap.TryGetValue(id, out var bubble))
                {
                    bubble.ReadingOrderIndex = order++;
                    updated.Add(bubble);
                }
            }

            // Also account for any bubbles on page not in the list (preserve them at the end)
            foreach (var remaining in pageBubbles.Where(b => !orderedBubbleIds.Contains(b.Id)).OrderBy(b => b.ReadingOrderIndex))
            {
                remaining.ReadingOrderIndex = order++;
                updated.Add(remaining);
            }

            await _bubbleRepository.UpdateOrderAsync(updated, cancellationToken);
            await _bubbleRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Reordered {Count} dialogues on page {PageId}", updated.Count, pageId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reorder dialogues on page {PageId}", pageId);
            return Result.Failure($"Failed to reorder dialogues: {ex.Message}");
        }
    }

    public async Task<Result> DeleteDialogueAsync(
        Guid bubbleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bubble = await _bubbleRepository.GetByIdAsync(bubbleId, cancellationToken);
            if (bubble == null)
            {
                return Result.Success(); // Already deleted
            }

            var pageId = bubble.PageId;
            await _bubbleRepository.DeleteAsync(bubbleId, cancellationToken);
            await _bubbleRepository.SaveChangesAsync(cancellationToken);

            // Re-sequence remaining bubbles on page to maintain gapless 1..N order
            var remaining = await _bubbleRepository.GetByPageIdAsync(pageId, cancellationToken);
            int order = 1;
            foreach (var b in remaining.OrderBy(b => b.ReadingOrderIndex))
            {
                b.ReadingOrderIndex = order++;
            }
            await _bubbleRepository.UpdateOrderAsync(remaining, cancellationToken);
            await _bubbleRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted dialogue {BubbleId} and re-sequenced page {PageId}", bubbleId, pageId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete dialogue {BubbleId}", bubbleId);
            return Result.Failure($"Failed to delete dialogue: {ex.Message}");
        }
    }
}
