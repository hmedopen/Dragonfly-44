using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Presentation.ViewModels;

public class ProjectItemViewModel : ViewModelBase
{
    private string _name;
    private string? _description;
    private DateTime _updatedAtUtc;
    private int _chapterCount;
    private int _pageCount;
    private bool _isEditing;
    private string _editName = string.Empty;

    public Guid Id { get; }
    public string SourceLanguage { get; }
    public string TargetLanguage { get; }
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

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
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

    public int ChapterCount
    {
        get => _chapterCount;
        set => SetProperty(ref _chapterCount, value);
    }

    public int PageCount
    {
        get => _pageCount;
        set => SetProperty(ref _pageCount, value);
    }

    public string FormattedUpdated => $"Updated {UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
    public string FormattedLanguages => $"{SourceLanguage.ToUpperInvariant()} → {TargetLanguage.ToUpperInvariant()}";

    public ProjectItemViewModel(Project project)
    {
        Id = project.Id;
        _name = project.Name;
        SourceLanguage = project.SourceLanguage;
        TargetLanguage = project.TargetLanguage;
        _description = project.Description;
        CreatedAtUtc = project.CreatedAtUtc;
        _updatedAtUtc = project.UpdatedAtUtc;
        _chapterCount = project.Chapters?.Count ?? 0;
        _pageCount = project.Chapters?.Sum(c => c.Pages?.Count ?? 0) ?? 0;
    }
}
