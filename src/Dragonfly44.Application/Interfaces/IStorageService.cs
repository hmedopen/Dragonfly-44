namespace Dragonfly44.Application.Interfaces;

public interface IStorageService
{
    string GetManagedStorageRoot();
    string ResolveAbsolutePath(string relativePath);
    Task<string> StoreChapterPageAsync(Guid projectId, Guid chapterId, string sourceFilePath, string targetFileName, CancellationToken cancellationToken = default);
    Task<string> StoreDraftPageAsync(Guid draftId, string sourceFilePath, string targetFileName, CancellationToken cancellationToken = default);
    Task DeleteChapterStorageAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default);
    Task DeleteProjectStorageAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task DeleteDraftStorageAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default);
}
