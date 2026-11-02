using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;

namespace Dragonfly44.Presentation.ViewModels;

public class DraftPageItemViewModel : ViewModelBase
{
    public Guid Id { get; }
    public Guid DraftId { get; }
    public int PageNumber { get; }
    public string OriginalFileName { get; }
    public string ImageRelativePath { get; }
    public string FileFormat { get; }
    public long FileSizeBytes { get; }
    public int Width { get; }
    public int Height { get; }
    public PageStatus Status { get; }
    public DateTime CreatedAtUtc { get; }

    public string FormattedPageNumber => $"Page {PageNumber:D2}";
    public string FormattedDimensions => (Width > 0 && Height > 0) ? $"{Width} × {Height} px" : "Dimensions pending";
    public string FormattedFileSize => FormatBytes(FileSizeBytes);
    public string FormattedImported => CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public DraftPageItemViewModel(DraftPage page)
    {
        Id = page.Id;
        DraftId = page.DraftId;
        PageNumber = page.PageNumber;
        OriginalFileName = page.OriginalFileName;
        ImageRelativePath = page.ImageRelativePath;
        FileFormat = page.FileFormat;
        FileSizeBytes = page.FileSizeBytes;
        Width = page.Width;
        Height = page.Height;
        Status = page.Status;
        CreatedAtUtc = page.CreatedAtUtc;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
    }
}
