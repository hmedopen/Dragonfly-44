using Dragonfly44.Application.Common;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using Dragonfly44.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Infrastructure.Services;

public class MangaIngestionService : IMangaIngestionService
{
    private readonly Dragonfly44DbContext _dbContext;
    private readonly IChapterRepository _chapterRepository;
    private readonly IPageRepository _pageRepository;
    private readonly IDraftRepository _draftRepository;
    private readonly IStorageService _storageService;
    private readonly IImageMetadataReader _imageMetadataReader;
    private readonly ILogger<MangaIngestionService> _logger;

    public MangaIngestionService(
        Dragonfly44DbContext dbContext,
        IChapterRepository chapterRepository,
        IPageRepository pageRepository,
        IDraftRepository draftRepository,
        IStorageService storageService,
        IImageMetadataReader imageMetadataReader,
        ILogger<MangaIngestionService> logger)
    {
        _dbContext = dbContext;
        _chapterRepository = chapterRepository;
        _pageRepository = pageRepository;
        _draftRepository = draftRepository;
        _storageService = storageService;
        _imageMetadataReader = imageMetadataReader;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<Page>>> IngestChapterFolderAsync(
        Guid projectId,
        Guid chapterId,
        string folderPath,
        IProgress<IngestionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return Result<IReadOnlyList<Page>>.Failure($"Folder does not exist: {folderPath}");
        }

        var chapter = await _chapterRepository.GetByIdAsync(chapterId, cancellationToken);
        if (chapter == null || chapter.ProjectId != projectId)
        {
            return Result<IReadOnlyList<Page>>.Failure($"Chapter with ID {chapterId} belonging to project {projectId} was not found.");
        }

        var supportedFiles = DiscoverSupportedImages(folderPath);
        if (supportedFiles.Count == 0)
        {
            return Result<IReadOnlyList<Page>>.Failure("No supported image files (.png, .jpg, .jpeg, .webp) found in the specified folder.");
        }

        // Sort filenames using mandatory natural sort
        var sortedFiles = supportedFiles
            .OrderBy(f => Path.GetFileName(f), NaturalStringComparer.Instance)
            .ToList();

        var existingPages = await _pageRepository.GetByChapterIdAsync(chapterId, cancellationToken);
        int nextPageNumber = existingPages.Count > 0 ? existingPages.Max(p => p.PageNumber) + 1 : 1;

        var copiedRelativePaths = new List<string>();
        var pagesToCreate = new List<Page>();

        try
        {
            for (int i = 0; i < sortedFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = sortedFiles[i];
                var originalName = Path.GetFileName(filePath);

                var metadata = _imageMetadataReader.ReadMetadata(filePath);

                var storedRelativePath = await _storageService.StoreChapterPageAsync(
                    projectId,
                    chapterId,
                    filePath,
                    originalName,
                    cancellationToken);

                copiedRelativePaths.Add(storedRelativePath);

                var page = new Page
                {
                    Id = Guid.NewGuid(),
                    ChapterId = chapterId,
                    PageNumber = nextPageNumber++,
                    OriginalFileName = originalName,
                    ImageRelativePath = storedRelativePath,
                    FileFormat = metadata.Format,
                    FileSizeBytes = metadata.FileSizeBytes,
                    Width = metadata.Width,
                    Height = metadata.Height,
                    Status = PageStatus.Pending,
                    CreatedAtUtc = DateTime.UtcNow
                };

                pagesToCreate.Add(page);

                progress?.Report(new IngestionProgress(
                    sortedFiles.Count,
                    i + 1,
                    originalName,
                    (double)(i + 1) / sortedFiles.Count * 100.0));
            }

            // Persist atomically in database
            using (var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
            {
                await _pageRepository.AddRangeAsync(pagesToCreate, cancellationToken);
                await _pageRepository.SaveChangesAsync(cancellationToken);

                chapter.UpdatedAtUtc = DateTime.UtcNow;
                await _chapterRepository.UpdateAsync(chapter, cancellationToken);
                await _chapterRepository.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("Successfully ingested {Count} pages for chapter {ChapterId}", pagesToCreate.Count, chapterId);
            return Result<IReadOnlyList<Page>>.Success(pagesToCreate);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Ingestion cancelled for chapter {ChapterId}. Rolling back stored files...", chapterId);
            await RollbackCopiedFilesAsync(copiedRelativePaths);
            return Result<IReadOnlyList<Page>>.Failure("Ingestion operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion failed for chapter {ChapterId}. Rolling back stored files...", chapterId);
            await RollbackCopiedFilesAsync(copiedRelativePaths);
            return Result<IReadOnlyList<Page>>.Failure($"Ingestion failed: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<DraftPage>>> IngestDraftFolderAsync(
        Guid draftId,
        string folderPath,
        IProgress<IngestionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return Result<IReadOnlyList<DraftPage>>.Failure($"Folder does not exist: {folderPath}");
        }

        var draft = await _draftRepository.GetByIdAsync(draftId, cancellationToken);
        if (draft == null)
        {
            return Result<IReadOnlyList<DraftPage>>.Failure($"Draft with ID {draftId} was not found.");
        }

        var supportedFiles = DiscoverSupportedImages(folderPath);
        if (supportedFiles.Count == 0)
        {
            return Result<IReadOnlyList<DraftPage>>.Failure("No supported image files (.png, .jpg, .jpeg, .webp) found in the specified folder.");
        }

        var sortedFiles = supportedFiles
            .OrderBy(f => Path.GetFileName(f), NaturalStringComparer.Instance)
            .ToList();

        int nextPageNumber = draft.Pages.Count > 0 ? draft.Pages.Max(p => p.PageNumber) + 1 : 1;

        var copiedRelativePaths = new List<string>();
        var draftPagesToCreate = new List<DraftPage>();

        try
        {
            for (int i = 0; i < sortedFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = sortedFiles[i];
                var originalName = Path.GetFileName(filePath);

                var metadata = _imageMetadataReader.ReadMetadata(filePath);

                var storedRelativePath = await _storageService.StoreDraftPageAsync(
                    draftId,
                    filePath,
                    originalName,
                    cancellationToken);

                copiedRelativePaths.Add(storedRelativePath);

                var draftPage = new DraftPage
                {
                    Id = Guid.NewGuid(),
                    DraftId = draftId,
                    PageNumber = nextPageNumber++,
                    OriginalFileName = originalName,
                    ImageRelativePath = storedRelativePath,
                    FileFormat = metadata.Format,
                    FileSizeBytes = metadata.FileSizeBytes,
                    Width = metadata.Width,
                    Height = metadata.Height,
                    Status = PageStatus.Pending,
                    CreatedAtUtc = DateTime.UtcNow
                };

                draftPagesToCreate.Add(draftPage);

                progress?.Report(new IngestionProgress(
                    sortedFiles.Count,
                    i + 1,
                    originalName,
                    (double)(i + 1) / sortedFiles.Count * 100.0));
            }

            using (var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
            {
                await _dbContext.DraftPages.AddRangeAsync(draftPagesToCreate, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                draft.UpdatedAtUtc = DateTime.UtcNow;
                await _draftRepository.UpdateAsync(draft, cancellationToken);
                await _draftRepository.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("Successfully ingested {Count} pages for draft {DraftId}", draftPagesToCreate.Count, draftId);
            return Result<IReadOnlyList<DraftPage>>.Success(draftPagesToCreate);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Ingestion cancelled for draft {DraftId}. Rolling back stored files...", draftId);
            await RollbackCopiedFilesAsync(copiedRelativePaths);
            return Result<IReadOnlyList<DraftPage>>.Failure("Ingestion operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion failed for draft {DraftId}. Rolling back stored files...", draftId);
            await RollbackCopiedFilesAsync(copiedRelativePaths);
            return Result<IReadOnlyList<DraftPage>>.Failure($"Ingestion failed: {ex.Message}");
        }
    }

    private List<string> DiscoverSupportedImages(string folderPath)
    {
        var result = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly))
        {
            if (_imageMetadataReader.IsSupportedImage(file))
            {
                result.Add(file);
            }
        }
        return result;
    }

    private async Task RollbackCopiedFilesAsync(IEnumerable<string> relativePaths)
    {
        foreach (var relPath in relativePaths)
        {
            try
            {
                await _storageService.DeleteFileAsync(relPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up file during rollback: {Path}", relPath);
            }
        }
    }
}
