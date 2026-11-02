using Dragonfly44.Application.Common;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Infrastructure.Services;

public class ChapterService : IChapterService
{
    private readonly IChapterRepository _chapterRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStorageService _storageService;
    private readonly ILogger<ChapterService> _logger;

    public ChapterService(
        IChapterRepository chapterRepository,
        IProjectRepository projectRepository,
        IStorageService storageService,
        ILogger<ChapterService> logger)
    {
        _chapterRepository = chapterRepository;
        _projectRepository = projectRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Result<Chapter>> CreateChapterAsync(
        Guid projectId,
        string title,
        int? chapterNumber = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<Chapter>.Failure("Chapter title cannot be empty.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
        {
            return Result<Chapter>.Failure($"Project with ID {projectId} does not exist.");
        }

        var existingChapters = await _chapterRepository.GetByProjectIdAsync(projectId, cancellationToken);

        int assignedNumber;
        if (chapterNumber.HasValue && chapterNumber.Value > 0)
        {
            assignedNumber = chapterNumber.Value;
            if (existingChapters.Any(c => c.ChapterNumber == assignedNumber))
            {
                return Result<Chapter>.Failure($"Chapter number {assignedNumber} already exists in this project.");
            }
        }
        else
        {
            assignedNumber = existingChapters.Count > 0
                ? existingChapters.Max(c => c.ChapterNumber) + 1
                : 1;
        }

        var chapter = new Chapter
        {
            ProjectId = projectId,
            ChapterNumber = assignedNumber,
            Title = title.Trim(),
            Status = ChapterStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _chapterRepository.AddAsync(chapter, cancellationToken);
            await _chapterRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created chapter {ChapterId} #{Number} '{Title}' for project {ProjectId}",
                chapter.Id, chapter.ChapterNumber, chapter.Title, projectId);
            return Result<Chapter>.Success(chapter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chapter '{Title}' for project {ProjectId}", title, projectId);
            return Result<Chapter>.Failure($"Failed to create chapter: {ex.Message}");
        }
    }

    public async Task<Result<Chapter>> RenameChapterAsync(
        Guid chapterId,
        string newTitle,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            return Result<Chapter>.Failure("Chapter title cannot be empty.");
        }

        try
        {
            var chapter = await _chapterRepository.GetByIdAsync(chapterId, cancellationToken);
            if (chapter == null)
            {
                return Result<Chapter>.Failure($"Chapter with ID {chapterId} was not found.");
            }

            chapter.Title = newTitle.Trim();
            chapter.UpdatedAtUtc = DateTime.UtcNow;

            await _chapterRepository.UpdateAsync(chapter, cancellationToken);
            await _chapterRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Renamed chapter {ChapterId} to '{Title}'", chapterId, chapter.Title);
            return Result<Chapter>.Success(chapter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename chapter {ChapterId}", chapterId);
            return Result<Chapter>.Failure($"Failed to rename chapter: {ex.Message}");
        }
    }

    public async Task<Result> DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default)
    {
        try
        {
            var chapter = await _chapterRepository.GetByIdAsync(chapterId, cancellationToken);
            if (chapter == null)
            {
                return Result.Success(); // Already deleted
            }

            var projectId = chapter.ProjectId;
            await _chapterRepository.DeleteAsync(chapterId, cancellationToken);
            await _chapterRepository.SaveChangesAsync(cancellationToken);

            // Clean up managed files on disk
            await _storageService.DeleteChapterStorageAsync(projectId, chapterId, cancellationToken);

            _logger.LogInformation("Deleted chapter {ChapterId} and cleaned storage", chapterId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chapter {ChapterId}", chapterId);
            return Result.Failure(ex.Message);
        }
    }

    public Task<Chapter?> GetChapterAsync(Guid chapterId, CancellationToken cancellationToken = default) =>
        _chapterRepository.GetByIdAsync(chapterId, cancellationToken);

    public Task<IReadOnlyList<Chapter>> GetChaptersByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _chapterRepository.GetByProjectIdAsync(projectId, cancellationToken);
}
