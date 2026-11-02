using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Dragonfly44.Presentation.Input;

public class DragDropCoordinator : IDragDropService
{
    private readonly ILogger<DragDropCoordinator> _logger;

    public event EventHandler<DragDropFilesEventArgs>? FilesDropped;

    public DragDropCoordinator(ILogger<DragDropCoordinator> logger)
    {
        _logger = logger;
    }

    public void RegisterDropTarget(UIElement element, IEnumerable<string>? allowedExtensions = null)
    {
        element.AllowDrop = true;
        element.DragOver += (s, e) =>
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        };

        element.Drop += async (s, e) =>
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var def = e.GetDeferral();
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    var filePaths = new List<string>();

                    foreach (var item in items)
                    {
                        if (item is StorageFile file)
                        {
                            if (allowedExtensions == null || IsSupportedExtension(file.Path, allowedExtensions))
                            {
                                filePaths.Add(file.Path);
                            }
                        }
                        else if (item is StorageFolder folder)
                        {
                            filePaths.Add(folder.Path);
                        }
                    }

                    if (filePaths.Count > 0)
                    {
                        NotifyFilesDropped(filePaths);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing dropped items");
                }
                finally
                {
                    def.Complete();
                }
            }
        };
    }

    public bool IsSupportedExtension(string filePath, IEnumerable<string> allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var ext = Path.GetExtension(filePath);
        return allowedExtensions.Any(e => string.Equals(e.TrimStart('.'), ext.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
    }

    public void NotifyFilesDropped(IEnumerable<string> filePaths)
    {
        var paths = filePaths.ToList();
        _logger.LogInformation("Dropped {Count} files: {Paths}", paths.Count, string.Join(", ", paths));
        FilesDropped?.Invoke(this, new DragDropFilesEventArgs(paths));
    }
}
