using System.Collections.ObjectModel;
using Dragonfly44.Application.Common;
using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Application.Synchronization;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;

namespace Dragonfly44.Presentation.ViewModels;

public enum DialogueFilterMode
{
    All = 0,
    Flagged = 1
}

/// <summary>
/// Master coordinator for the Dialogue Feed, handling filtering, selection, editing states,
/// drag-and-drop reordering, page divider grouping, and synchronization with the manga canvas.
/// </summary>
public class DialogueFeedViewModel : ViewModelBase
{
    private readonly IDialogueService _dialogueService;
    private readonly IDialogService _dialogPresenter;
    private readonly ILogger<DialogueFeedViewModel> _logger;
    private readonly ITranslationService? _translationService;

    private Guid _currentChapterId;
    private DialogueFilterMode _filterMode = DialogueFilterMode.All;
    private DialogueCardViewModel? _selectedCard;
    private DialogueCardViewModel? _activeEditingCard;
    private string _statusMessage = string.Empty;
    private bool _isLoading;
    private int _activePageNumber = 1;

    // Master flat list of all cards for the chapter
    private readonly List<DialogueCardViewModel> _allCards = new();

    public IWorkspaceSyncCoordinator? SyncCoordinator { get; private set; }

    // Grouped by page for page divider headers
    public ObservableCollection<DialogueFeedGroupViewModel> PageGroups { get; } = new();

    // Filtered flat collection for virtualized feed display
    public ObservableCollection<DialogueCardViewModel> VisibleCards { get; } = new();

    public int ActivePageNumber
    {
        get => _activePageNumber;
        set
        {
            if (SetProperty(ref _activePageNumber, value))
            {
                foreach (var group in PageGroups)
                {
                    group.IsActivePage = (group.PageNumber == _activePageNumber);
                }
            }
        }
    }

    public DialogueFilterMode FilterMode
    {
        get => _filterMode;
        set
        {
            if (SetProperty(ref _filterMode, value))
            {
                OnPropertyChanged(nameof(IsAllFilterActive));
                OnPropertyChanged(nameof(IsFlaggedFilterActive));
                OnPropertyChanged(nameof(FilterAllBackground));
                OnPropertyChanged(nameof(FilterFlaggedBackground));
                RefreshVisibleCards();
            }
        }
    }

    public bool IsAllFilterActive => FilterMode == DialogueFilterMode.All;
    public bool IsFlaggedFilterActive => FilterMode == DialogueFilterMode.Flagged;

    public Brush FilterAllBackground
    {
        get
        {
            var app = Microsoft.UI.Xaml.Application.Current;
            if (app == null) return null!;
            return FilterMode == DialogueFilterMode.All
                ? (Brush)app.Resources["DragonflyPlaceholderHoverBrush"]
                : (Brush)app.Resources["DragonflyPlaceholderSurfaceBrush"];
        }
    }

    public Brush FilterFlaggedBackground
    {
        get
        {
            var app = Microsoft.UI.Xaml.Application.Current;
            if (app == null) return null!;
            return FilterMode == DialogueFilterMode.Flagged
                ? (Brush)app.Resources["DragonflyPlaceholderHoverBrush"]
                : (Brush)app.Resources["DragonflyPlaceholderSurfaceBrush"];
        }
    }

    public DialogueCardViewModel? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (_selectedCard != value)
            {
                if (_selectedCard != null) _selectedCard.IsSelected = false;
                _selectedCard = value;
                if (_selectedCard != null) _selectedCard.IsSelected = true;

                OnPropertyChanged(nameof(SelectedCard));
                OnPropertyChanged(nameof(HasSelectedCard));

                if (_selectedCard != null)
                {
                    CardSelected?.Invoke(this, _selectedCard);
                    SyncCoordinator?.SelectBubble(_selectedCard.Id, _selectedCard.PageNumber, SelectionOrigin.DialogueFeed);
                }
                else
                {
                    SyncCoordinator?.ClearSelection(SelectionOrigin.DialogueFeed);
                }
            }
        }
    }

    public void SetSelectedCardSilently(DialogueCardViewModel? card)
    {
        if (_selectedCard != card)
        {
            if (_selectedCard != null) _selectedCard.IsSelected = false;
            _selectedCard = card;
            if (_selectedCard != null) _selectedCard.IsSelected = true;

            OnPropertyChanged(nameof(SelectedCard));
            OnPropertyChanged(nameof(HasSelectedCard));

            if (_selectedCard != null)
            {
                CardSelected?.Invoke(this, _selectedCard);
            }
        }
    }

    public bool HasSelectedCard => SelectedCard != null;

    public DialogueCardViewModel? ActiveEditingCard
    {
        get => _activeEditingCard;
        set
        {
            if (_activeEditingCard != value)
            {
                if (_activeEditingCard != null)
                {
                    _activeEditingCard.IsEditing = false;
                    _ = SaveCardEditsAsync(_activeEditingCard);
                }

                _activeEditingCard = value;
                if (_activeEditingCard != null)
                {
                    _activeEditingCard.IsEditing = true;
                    SelectedCard = _activeEditingCard;
                }

                OnPropertyChanged(nameof(ActiveEditingCard));
                OnPropertyChanged(nameof(IsEditingActive));

                SyncCoordinator?.SetActiveEditing(_activeEditingCard?.Id, SelectionOrigin.DialogueFeed);
            }
        }
    }

    public bool IsEditingActive => ActiveEditingCard != null;

    public int TotalCardCount => _allCards.Count;
    public int FlaggedCardCount => _allCards.Count(c => c.IsFlagged);
    public string SummaryText => $"{TotalCardCount} dialogue bubbles ({FlaggedCardCount} flagged)";

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // Events for Canvas and View integration
    public event EventHandler<DialogueCardViewModel>? CardSelected;
    public event EventHandler<int>? NavigateToPageRequested;
    public event EventHandler<Bubble>? DialogueCreated;
    public event EventHandler<Guid>? DialogueDeleted;
    public event EventHandler<(Guid PageId, IReadOnlyList<Guid> OrderedIds)>? DialoguesReordered;

    public DialogueFeedViewModel(
        IDialogueService dialogueService,
        IDialogService dialogPresenter,
        ILogger<DialogueFeedViewModel> logger,
        IWorkspaceSyncCoordinator? syncCoordinator = null,
        ITranslationService? translationService = null)
    {
        _dialogueService = dialogueService;
        _dialogPresenter = dialogPresenter;
        _logger = logger;
        _translationService = translationService;
        AttachSyncCoordinator(syncCoordinator);
    }

    public void AttachSyncCoordinator(IWorkspaceSyncCoordinator? syncCoordinator)
    {
        if (SyncCoordinator != null)
        {
            SyncCoordinator.SelectionChanged -= OnCoordinatorSelectionChanged;
            SyncCoordinator.PageChanged -= OnCoordinatorPageChanged;
            SyncCoordinator.ChapterReset -= OnCoordinatorChapterReset;
        }

        SyncCoordinator = syncCoordinator;

        if (SyncCoordinator != null)
        {
            SyncCoordinator.SelectionChanged += OnCoordinatorSelectionChanged;
            SyncCoordinator.PageChanged += OnCoordinatorPageChanged;
            SyncCoordinator.ChapterReset += OnCoordinatorChapterReset;
        }
    }

    private void OnCoordinatorSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.SelectedBubbleId.HasValue)
        {
            var card = _allCards.FirstOrDefault(c => c.Id == e.SelectedBubbleId.Value);
            if (card != null)
            {
                if (FilterMode == DialogueFilterMode.Flagged && !card.IsFlagged)
                {
                    FilterMode = DialogueFilterMode.All;
                }
                SetSelectedCardSilently(card);
            }
            else
            {
                SetSelectedCardSilently(null);
            }
        }
        else
        {
            SetSelectedCardSilently(null);
        }
    }

    private void OnCoordinatorPageChanged(object? sender, PageChangedEventArgs e)
    {
        if (ActivePageNumber != e.PageNumber)
        {
            if (ActiveEditingCard != null && ActiveEditingCard.PageNumber != e.PageNumber)
            {
                _ = SaveCardEditsAsync(ActiveEditingCard);
            }

            ActivePageNumber = e.PageNumber;
        }
    }

    private void OnCoordinatorChapterReset(object? sender, EventArgs e)
    {
        SetSelectedCardSilently(null);
        ActiveEditingCard = null;
    }

    /// <summary>
    /// Loads all dialogue cards for the given chapter and pages.
    /// </summary>
    public async Task LoadChapterDialoguesAsync(
        Guid chapterId,
        IReadOnlyList<Page> pages,
        CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        _currentChapterId = chapterId;
        _allCards.Clear();
        PageGroups.Clear();

        try
        {
            var pageMap = pages.ToDictionary(p => p.Id);

            // Create page groups in numeric order
            foreach (var p in pages.OrderBy(p => p.PageNumber))
            {
                var group = new DialogueFeedGroupViewModel(p.Id, p.PageNumber)
                {
                    IsActivePage = (p.PageNumber == ActivePageNumber)
                };

                group.RequestAddDialogueToPage += async (s, e) => await AddNewDialogueToPageAsync(group.PageId);
                group.RequestNavigateToPage += (s, e) =>
                {
                    NavigateToPageRequested?.Invoke(this, group.PageNumber);
                    SyncCoordinator?.SetActivePage(group.PageNumber, SelectionOrigin.DialogueFeed);
                };
                PageGroups.Add(group);
            }

            var result = await _dialogueService.GetDialogueByChapterAsync(chapterId, cancellationToken);
            if (result.IsSuccess && result.Value != null)
            {
                var groupsById = PageGroups.ToDictionary(g => g.PageId);

                int globalDisplayNumber = 1;
                foreach (var bubble in result.Value)
                {
                    var pageNum = pageMap.TryGetValue(bubble.PageId, out var pg) ? pg.PageNumber : 1;
                    var cardVm = CreateCardViewModel(bubble, pageNum, globalDisplayNumber++);
                    _allCards.Add(cardVm);

                    if (groupsById.TryGetValue(bubble.PageId, out var grp))
                    {
                        grp.Cards.Add(cardVm);
                    }
                }
            }

            RefreshVisibleCards();
            StatusMessage = $"Loaded {_allCards.Count} dialogue cards.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dialogues for chapter {ChapterId}", chapterId);
            StatusMessage = $"Error loading dialogues: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private DialogueCardViewModel CreateCardViewModel(Bubble bubble, int pageNumber, int displayNumber)
    {
        var vm = new DialogueCardViewModel(bubble, pageNumber, displayNumber);

        vm.RequestSelect += (s, e) => SelectedCard = vm;
        vm.RequestSave += async (s, e) => await SaveCardEditsAsync(vm);
        vm.RequestToggleFlag += async (s, e) => await ToggleFlagAsync(vm);
        vm.RequestDelete += async (s, e) => await ConfirmAndDeleteCardAsync(vm);
        vm.RequestMoveUp += async (s, e) => await MoveCardUpAsync(vm);
        vm.RequestMoveDown += async (s, e) => await MoveCardDownAsync(vm);
        vm.RequestAiTranslate += async (s, e) => await TranslateCardAsync(vm);
        vm.RequestAcceptAiTranslation += async (s, e) => await AcceptAiTranslationAsync(vm);
        vm.RequestRejectAiTranslation += async (s, e) => await RejectAiTranslationAsync(vm);
        vm.RequestRevertToManual += async (s, e) => await RevertToManualTranslationAsync(vm);

        return vm;
    }

    /// <summary>
    /// Re-evaluates VisibleCards based on current filter mode, preserving dynamic display numbering.
    /// Handles filtering out of selected cards safely without broken references.
    /// </summary>
    public void RefreshVisibleCards()
    {
        VisibleCards.Clear();

        var query = FilterMode switch
        {
            DialogueFilterMode.Flagged => _allCards.Where(c => c.IsFlagged),
            _ => _allCards
        };

        int displayNum = 1;
        foreach (var card in query)
        {
            card.DisplayNumber = displayNum++;
            VisibleCards.Add(card);
        }

        // If currently selected card was filtered out, safely clear selection
        if (SelectedCard != null && !VisibleCards.Contains(SelectedCard))
        {
            SelectedCard = null;
        }

        OnPropertyChanged(nameof(TotalCardCount));
        OnPropertyChanged(nameof(FlaggedCardCount));
        OnPropertyChanged(nameof(SummaryText));
    }

    // -------------------------------------------------------------
    // CARD ACTIONS (SAVE, FLAG, CLASSIFY, DELETE, ADD)
    // -------------------------------------------------------------

    public async Task SaveCardEditsAsync(DialogueCardViewModel card)
    {
        try
        {
            var result = await _dialogueService.UpdateDialogueTextAsync(card.Id, card.RawText, card.TranslatedText);
            if (result.IsSuccess && result.Value != null)
            {
                card.ShapeType = result.Value.ShapeType;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save edits for card {Id}", card.Id);
        }
    }

    public async Task ToggleFlagAsync(DialogueCardViewModel card)
    {
        var result = await _dialogueService.ToggleFlaggedAsync(card.Id);
        if (result.IsSuccess && result.Value != null)
        {
            card.IsFlagged = result.Value.IsFlagged;
            if (FilterMode == DialogueFilterMode.Flagged && !card.IsFlagged)
            {
                RefreshVisibleCards();
            }
            else
            {
                OnPropertyChanged(nameof(FlaggedCardCount));
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public async Task ChangeClassificationAsync(DialogueCardViewModel card, BubbleShapeType shapeType)
    {
        var result = await _dialogueService.UpdateClassificationAsync(card.Id, shapeType);
        if (result.IsSuccess && result.Value != null)
        {
            card.ShapeType = shapeType;
        }
    }

    // -------------------------------------------------------------
    // AI TRANSLATION ACTIONS (TRANSLATE, ACCEPT, REJECT, REVERT)
    // -------------------------------------------------------------

    public async Task TranslateCardAsync(DialogueCardViewModel card, CancellationToken cancellationToken = default)
    {
        if (_translationService == null)
        {
            card.TranslationError = "AI Translation Service is not configured.";
            return;
        }

        if (string.IsNullOrWhiteSpace(card.RawText))
        {
            card.TranslationError = "Source text is empty.";
            return;
        }

        card.IsTranslating = true;
        card.TranslationError = null;

        try
        {
            var result = await _translationService.TranslateDialogueAsync(card.Id, cancellationToken: cancellationToken);
            if (result.IsSuccess && !string.IsNullOrEmpty(result.TranslatedText))
            {
                var notes = result.Notes;
                card.SetPendingTranslation(result.TranslatedText, notes, result.HistoryEntryId);
                StatusMessage = $"Translated card #{card.DisplayNumber} via AI (Cost: ${result.EstimatedCostUsd:F5})";
            }
            else
            {
                card.TranslationError = result.ErrorMessage ?? "Translation failed.";
                card.IsTranslating = false;
                StatusMessage = $"Translation error: {card.TranslationError}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate dialogue card {CardId}", card.Id);
            card.TranslationError = ex.Message;
            card.IsTranslating = false;
        }
    }

    public async Task AcceptAiTranslationAsync(DialogueCardViewModel card, CancellationToken cancellationToken = default)
    {
        if (card.PendingHistoryEntryId.HasValue && _translationService != null)
        {
            await _translationService.AcceptTranslationAsync(card.PendingHistoryEntryId.Value, cancellationToken);
        }

        if (!string.IsNullOrEmpty(card.PendingAiTranslation))
        {
            card.ApplyAcceptedTranslation(card.PendingAiTranslation);
            StatusMessage = $"Accepted AI translation for card #{card.DisplayNumber}.";
        }
    }

    public async Task RejectAiTranslationAsync(DialogueCardViewModel card, CancellationToken cancellationToken = default)
    {
        if (card.PendingHistoryEntryId.HasValue && _translationService != null)
        {
            await _translationService.RejectTranslationAsync(card.PendingHistoryEntryId.Value, cancellationToken);
        }

        card.ClearPendingTranslation();
        StatusMessage = $"Discarded AI translation for card #{card.DisplayNumber}.";
    }

    public async Task RevertToManualTranslationAsync(DialogueCardViewModel card, CancellationToken cancellationToken = default)
    {
        if (_translationService != null)
        {
            await _translationService.RevertToManualTranslationAsync(card.Id, cancellationToken);
        }

        if (!string.IsNullOrEmpty(card.LastManualTranslation))
        {
            card.ApplyRevertedTranslation(card.LastManualTranslation);
            StatusMessage = $"Reverted card #{card.DisplayNumber} to previous manual translation.";
        }
    }

    public async Task AddNewDialogueToPageAsync(Guid pageId)
    {
        var pageGroup = PageGroups.FirstOrDefault(g => g.PageId == pageId);
        int pageNumber = pageGroup?.PageNumber ?? 1;

        var result = await _dialogueService.CreateDialogueAsync(
            pageId,
            rawText: string.Empty,
            translatedText: string.Empty,
            shapeType: BubbleShapeType.StandardSpeech);

        if (result.IsSuccess && result.Value != null)
        {
            var cardVm = CreateCardViewModel(result.Value, pageNumber, _allCards.Count + 1);
            _allCards.Add(cardVm);
            pageGroup?.Cards.Add(cardVm);

            RefreshVisibleCards();
            SelectedCard = cardVm;
            ActiveEditingCard = cardVm;
            StatusMessage = $"Added dialogue bubble to Page {pageNumber}.";

            DialogueCreated?.Invoke(this, result.Value);
        }
    }

    public async Task ConfirmAndDeleteCardAsync(DialogueCardViewModel card)
    {
        var dialogResult = await _dialogPresenter.ShowConfirmationAsync(
            "Delete Dialogue",
            $"Are you sure you want to delete dialogue #{card.DisplayNumber} from Page {card.PageNumber}?",
            confirmText: "Delete",
            cancelText: "Cancel",
            isDestructive: true);

        if (dialogResult != DialogResult.Primary) return;

        var result = await _dialogueService.DeleteDialogueAsync(card.Id);
        if (result.IsSuccess)
        {
            if (SelectedCard == card) SelectedCard = null;
            if (ActiveEditingCard == card) _activeEditingCard = null;

            _allCards.Remove(card);

            var pageGroup = PageGroups.FirstOrDefault(g => g.PageId == card.PageId);
            pageGroup?.Cards.Remove(card);

            RefreshVisibleCards();
            StatusMessage = "Dialogue deleted.";

            DialogueDeleted?.Invoke(this, card.Id);
        }
        else
        {
            await _dialogPresenter.ShowMessageAsync("Delete Failed", result.Error ?? "Could not delete dialogue.");
        }
    }

    // -------------------------------------------------------------
    // DRAG-AND-DROP & KEYBOARD REORDERING
    // -------------------------------------------------------------

    public async Task MoveCardUpAsync(DialogueCardViewModel card)
    {
        var group = PageGroups.FirstOrDefault(g => g.PageId == card.PageId);
        if (group == null) return;

        var idx = group.Cards.IndexOf(card);
        if (idx > 0)
        {
            group.Cards.Move(idx, idx - 1);
            await PersistGroupOrderAsync(group);
        }
    }

    public async Task MoveCardDownAsync(DialogueCardViewModel card)
    {
        var group = PageGroups.FirstOrDefault(g => g.PageId == card.PageId);
        if (group == null) return;

        var idx = group.Cards.IndexOf(card);
        if (idx >= 0 && idx < group.Cards.Count - 1)
        {
            group.Cards.Move(idx, idx + 1);
            await PersistGroupOrderAsync(group);
        }
    }

    public async Task ReorderCardsInPageAsync(Guid pageId, int oldIndex, int newIndex)
    {
        var group = PageGroups.FirstOrDefault(g => g.PageId == pageId);
        if (group == null || oldIndex < 0 || newIndex < 0 || oldIndex >= group.Cards.Count || newIndex >= group.Cards.Count) return;

        group.Cards.Move(oldIndex, newIndex);
        await PersistGroupOrderAsync(group);
    }

    private async Task PersistGroupOrderAsync(DialogueFeedGroupViewModel group)
    {
        var orderedIds = group.Cards.Select(c => c.Id).ToList();
        var result = await _dialogueService.ReorderDialoguesAsync(group.PageId, orderedIds);
        if (result.IsSuccess)
        {
            // Re-sync allCards order to match page sequence
            _allCards.Clear();
            foreach (var grp in PageGroups)
            {
                _allCards.AddRange(grp.Cards);
            }

            RefreshVisibleCards();
            StatusMessage = $"Reordered dialogues on Page {group.PageNumber}.";

            DialoguesReordered?.Invoke(this, (group.PageId, orderedIds));
        }
    }

    // -------------------------------------------------------------
    // CANVAS INTEGRATION
    // -------------------------------------------------------------

    /// <summary>
    /// Selects a dialogue card by bubble ID when clicked on the manga canvas.
    /// </summary>
    public void SelectCardById(Guid bubbleId)
    {
        var card = _allCards.FirstOrDefault(c => c.Id == bubbleId);
        if (card != null)
        {
            SelectedCard = card;
        }
    }

    // -------------------------------------------------------------
    // KEYBOARD NAVIGATION
    // -------------------------------------------------------------

    public void SelectNextCard()
    {
        if (VisibleCards.Count == 0) return;
        if (SelectedCard == null)
        {
            SelectedCard = VisibleCards[0];
            return;
        }

        var idx = VisibleCards.IndexOf(SelectedCard);
        if (idx >= 0 && idx < VisibleCards.Count - 1)
        {
            SelectedCard = VisibleCards[idx + 1];
        }
    }

    public void SelectPreviousCard()
    {
        if (VisibleCards.Count == 0) return;
        if (SelectedCard == null)
        {
            SelectedCard = VisibleCards[VisibleCards.Count - 1];
            return;
        }

        var idx = VisibleCards.IndexOf(SelectedCard);
        if (idx > 0)
        {
            SelectedCard = VisibleCards[idx - 1];
        }
    }

    public void StartEditingSelected()
    {
        if (SelectedCard != null)
        {
            ActiveEditingCard = SelectedCard;
        }
    }

    public void StopEditing()
    {
        ActiveEditingCard = null;
    }

    public async Task ToggleFlagSelectedAsync()
    {
        if (SelectedCard != null)
        {
            await ToggleFlagAsync(SelectedCard);
        }
    }

    public void Clear()
    {
        _allCards.Clear();
        PageGroups.Clear();
        VisibleCards.Clear();
        _selectedCard = null;
        _activeEditingCard = null;
        OnPropertyChanged(nameof(SelectedCard));
        OnPropertyChanged(nameof(HasSelectedCard));
        OnPropertyChanged(nameof(ActiveEditingCard));
        OnPropertyChanged(nameof(IsEditingActive));
        OnPropertyChanged(nameof(TotalCardCount));
        OnPropertyChanged(nameof(FlaggedCardCount));
        OnPropertyChanged(nameof(SummaryText));
    }
}
