namespace Dragonfly44.Application.Interfaces;

public record DragDropFilesEventArgs(IReadOnlyList<string> FilePaths);

public interface IDragDropService
{
    event EventHandler<DragDropFilesEventArgs> FilesDropped;

    bool IsSupportedExtension(string filePath, IEnumerable<string> allowedExtensions);
    void NotifyFilesDropped(IEnumerable<string> filePaths);
}
