using Dragonfly44.Application.Common;
using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Application.Services;

public record IngestionProgress(int TotalFiles, int ProcessedFiles, string CurrentFileName, double Percent);

public interface IMangaIngestionService
{
    Task<Result<IReadOnlyList<Page>>> IngestChapterFolderAsync(
        Guid projectId,
        Guid chapterId,
        string folderPath,
        IProgress<IngestionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DraftPage>>> IngestDraftFolderAsync(
        Guid draftId,
        string folderPath,
        IProgress<IngestionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
