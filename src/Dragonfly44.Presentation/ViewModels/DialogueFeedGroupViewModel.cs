using System.Collections.ObjectModel;
using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Presentation.ViewModels;

/// <summary>
/// Represents a manga page divider and grouping within the dialogue feed.
/// Enforces page association boundaries and provides quick navigation.
/// </summary>
public class DialogueFeedGroupViewModel : ViewModelBase
{
    private int _pageNumber;
    private bool _isActivePage;

    public Guid PageId { get; }

    public int PageNumber
    {
        get => _pageNumber;
        set
        {
            if (SetProperty(ref _pageNumber, value))
            {
                OnPropertyChanged(nameof(PageHeaderTitle));
            }
        }
    }

    public bool IsActivePage
    {
        get => _isActivePage;
        set => SetProperty(ref _isActivePage, value);
    }

    public string PageHeaderTitle => $"📄 Page {PageNumber}";

    public ObservableCollection<DialogueCardViewModel> Cards { get; } = new();

    public string CardCountText => Cards.Count == 1 ? "1 dialogue" : $"{Cards.Count} dialogues";

    public event EventHandler? RequestAddDialogueToPage;
    public event EventHandler? RequestNavigateToPage;

    public DialogueFeedGroupViewModel(Guid pageId, int pageNumber)
    {
        PageId = pageId;
        PageNumber = pageNumber;
        Cards.CollectionChanged += (s, e) => OnPropertyChanged(nameof(CardCountText));
    }

    public void TriggerAddDialogue() => RequestAddDialogueToPage?.Invoke(this, EventArgs.Empty);
    public void TriggerNavigateToPage() => RequestNavigateToPage?.Invoke(this, EventArgs.Empty);
}
