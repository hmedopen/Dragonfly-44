using Dragonfly44.Application.Interfaces;
using Dragonfly44.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dragonfly44.Infrastructure.Storage;

public class FileSystemStorageService : IStorageService
{
    private readonly string _storageRoot;
    private readonly ILogger<FileSystemStorageService> _logger;

    public FileSystemStorageService(
        IOptions<StorageOptions> storageOptions,
        ILogger<FileSystemStorageService> logger)
    {
        _logger = logger;
        var appData = storageOptions.Value.AppDataDirectory;
        if (string.IsNullOrWhiteSpace(appData))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            appData = Path.Combine(localAppData, "Dragonfly44");
        }

        _storageRoot = Path.Combine(appData, "storage");
        Directory.CreateDirectory(_storageRoot);
    }

    public string GetManagedStorageRoot() => _storageRoot;

    public string ResolveAbsolutePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }

        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_storageRoot, normalized);
    }

    public async Task<string> StoreChapterPageAsync(
        Guid projectId,
        Guid chapterId,
        string sourceFilePath,
        string targetFileName,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.Combine(
            _storageRoot,
            "projects",
            projectId.ToString("N"),
            "chapters",
            chapterId.ToString("N"),
            "pages");

        return await CopyFileToManagedStorageAsync(targetDir, $"projects/{projectId:N}/chapters/{chapterId:N}/pages", sourceFilePath, targetFileName, cancellationToken);
    }

    public async Task<string> StoreDraftPageAsync(
        Guid draftId,
        string sourceFilePath,
        string targetFileName,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.Combine(
            _storageRoot,
            "drafts",
            draftId.ToString("N"),
            "pages");

        return await CopyFileToManagedStorageAsync(targetDir, $"drafts/{draftId:N}/pages", sourceFilePath, targetFileName, cancellationToken);
    }

    private async Task<string> CopyFileToManagedStorageAsync(
        string targetDirectory,
        string relativeDirPrefix,
        string sourceFilePath,
        string desiredFileName,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Source image file not found: {sourceFilePath}", sourceFilePath);
        }

        Directory.CreateDirectory(targetDirectory);

        var safeFileName = SanitizeFileName(desiredFileName);
        var baseName = Path.GetFileNameWithoutExtension(safeFileName);
        var extension = Path.GetExtension(safeFileName);

        var finalFileName = safeFileName;
        var destinationPath = Path.Combine(targetDirectory, finalFileName);
        var counter = 1;

        // Prevent collision - never silently overwrite
        while (File.Exists(destinationPath))
        {
            finalFileName = $"{baseName} ({counter}){extension}";
            destinationPath = Path.Combine(targetDirectory, finalFileName);
            counter++;
        }

        try
        {
            // Copy byte-for-byte with cancellation support to avoid recompression
            const int bufferSize = 81920;
            using (var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
            using (var destinationStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
            {
                await sourceStream.CopyToAsync(destinationStream, bufferSize, cancellationToken);
            }
        }
        catch
        {
            try
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
            catch { }
            throw;
        }

        var relativePath = $"{relativeDirPrefix}/{finalFileName}";
        _logger.LogDebug("Stored page file at {DestinationPath} (relative: {RelativePath})", destinationPath, relativePath);
        return relativePath;
    }

    public Task DeleteChapterStorageAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default)
    {
        var chapterDir = Path.Combine(
            _storageRoot,
            "projects",
            projectId.ToString("N"),
            "chapters",
            chapterId.ToString("N"));

        try
        {
            if (Directory.Exists(chapterDir))
            {
                Directory.Delete(chapterDir, recursive: true);
                _logger.LogInformation("Deleted storage directory for chapter {ChapterId}", chapterId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to completely delete storage for chapter {ChapterId}", chapterId);
        }

        return Task.CompletedTask;
    }

    public Task DeleteProjectStorageAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var projectDir = Path.Combine(_storageRoot, "projects", projectId.ToString("N"));
        try
        {
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, recursive: true);
                _logger.LogInformation("Deleted storage directory for project {ProjectId}", projectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to completely delete storage for project {ProjectId}", projectId);
        }

        return Task.CompletedTask;
    }

    public Task DeleteDraftStorageAsync(Guid draftId, CancellationToken cancellationToken = default)
    {
        var draftDir = Path.Combine(_storageRoot, "drafts", draftId.ToString("N"));
        try
        {
            if (Directory.Exists(draftDir))
            {
                Directory.Delete(draftDir, recursive: true);
                _logger.LogInformation("Deleted storage directory for draft {DraftId}", draftId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to completely delete storage for draft {DraftId}", draftId);
        }

        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveAbsolutePath(relativePath);
        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogDebug("Deleted file {FullPath}", fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file {FullPath}", fullPath);
        }

        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? $"page_{Guid.NewGuid():N}.img" : cleaned;
    }
}
