using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;

namespace Dragonfly44.Presentation.ViewModels;

public class ChapterItemViewModel : ViewModelBase
{
    private string _title;
    private int _chapterNumber;
    private ChapterStatus _status;
    private DateTime _updatedAtUtc;
    private int _pageCount;
    private bool _isEditing;
    private string _editName = string.Empty;

    public Guid Id { get; }
    public Guid ProjectId { get; }
    public DateTime CreatedAtUtc { get; }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(NormalVisibility));
                OnPropertyChanged(nameof(EditingVisibility));
            }
        }
    }

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public Microsoft.UI.Xaml.Visibility NormalVisibility => IsEditing ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    public Microsoft.UI.Xaml.Visibility EditingVisibility => IsEditing ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public int ChapterNumber
    {
        get => _chapterNumber;
        set
        {
            if (SetProperty(ref _chapterNumber, value))
            {
                OnPropertyChanged(nameof(FormattedTitle));
            }
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                OnPropertyChanged(nameof(FormattedTitle));
            }
        }
    }

    public ChapterStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(FormattedStatus));
            }
        }
    }

    public DateTime UpdatedAtUtc
    {
        get => _updatedAtUtc;
        set
        {
            if (SetProperty(ref _updatedAtUtc, value))
            {
                OnPropertyChanged(nameof(FormattedUpdated));
            }
        }
    }

    public int PageCount
    {
        get => _pageCount;
        set
        {
            if (SetProperty(ref _pageCount, value))
            {
                OnPropertyChanged(nameof(FormattedPages));
            }
        }
    }

    public string FormattedTitle => $"Chapter {ChapterNumber}: {Title}";
    public string FormattedStatus => Status.ToString();
    public string FormattedPages => $"{PageCount} page{(PageCount == 1 ? "" : "s")}";
    public string FormattedUpdated => $"Updated {UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

    public ChapterItemViewModel(Chapter chapter)
    {
        Id = chapter.Id;
        ProjectId = chapter.ProjectId;
        _chapterNumber = chapter.ChapterNumber;
        _title = chapter.Title;
        _status = chapter.Status;
        CreatedAtUtc = chapter.CreatedAtUtc;
        _updatedAtUtc = chapter.UpdatedAtUtc;
        _pageCount = chapter.Pages?.Count ?? 0;
    }
}
