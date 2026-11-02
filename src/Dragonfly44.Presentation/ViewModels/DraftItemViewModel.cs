using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Presentation.ViewModels;

public class DraftItemViewModel : ViewModelBase
{
    private string _name;
    private string? _description;
    private DateTime _updatedAtUtc;
    private int _pageCount;
    private bool _isEditing;
    private string _editName = string.Empty;

    public Guid Id { get; }
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

    public string FormattedPages => $"{PageCount} page{(PageCount == 1 ? "" : "s")}";
    public string FormattedUpdated => $"Updated {UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

    public DraftItemViewModel(Draft draft)
    {
        Id = draft.Id;
        _name = draft.Name;
        _description = draft.Description;
        CreatedAtUtc = draft.CreatedAtUtc;
        _updatedAtUtc = draft.UpdatedAtUtc;
        _pageCount = draft.Pages?.Count ?? 0;
    }
}
