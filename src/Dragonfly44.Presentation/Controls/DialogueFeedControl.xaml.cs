using Dragonfly44.Domain.Enums;
using Dragonfly44.Presentation.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Dragonfly44.Presentation.Controls;

public sealed partial class DialogueFeedControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(DialogueFeedViewModel),
            typeof(DialogueFeedControl),
            new PropertyMetadata(null));

    public DialogueFeedViewModel? ViewModel
    {
        get => (DialogueFeedViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public DialogueFeedControl()
    {
        InitializeComponent();
    }

    // -------------------------------------------------------------
    // INTERACTION EVENT HANDLERS
    // -------------------------------------------------------------

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DialogueCardsListView.SelectedItem is DialogueCardViewModel card)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                DialogueCardsListView.ScrollIntoView(card);
            });
        }
    }

    private void OnAddDialogueClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        // Default to active page group, then selected card's page, then first page
        var targetPageId = ViewModel.PageGroups.FirstOrDefault(g => g.PageNumber == ViewModel.ActivePageNumber)?.PageId
            ?? ViewModel.SelectedCard?.PageId
            ?? ViewModel.PageGroups.FirstOrDefault()?.PageId;

        if (targetPageId.HasValue && targetPageId.Value != Guid.Empty)
        {
            _ = ViewModel.AddNewDialogueToPageAsync(targetPageId.Value);
        }
    }

    private void OnFilterAllClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) ViewModel.FilterMode = DialogueFilterMode.All;
    }

    private void OnFilterFlaggedClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) ViewModel.FilterMode = DialogueFilterMode.Flagged;
    }

    private void OnClassificationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.Tag is DialogueCardViewModel card && cb.SelectedIndex >= 0)
        {
            var newType = (BubbleShapeType)cb.SelectedIndex;
            if (card.ShapeType != newType && ViewModel != null)
            {
                _ = ViewModel.ChangeClassificationAsync(card, newType);
            }
        }
    }

    private void OnMoveUpClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DialogueCardViewModel card && ViewModel != null)
        {
            _ = ViewModel.MoveCardUpAsync(card);
        }
    }

    private void OnMoveDownClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DialogueCardViewModel card && ViewModel != null)
        {
            _ = ViewModel.MoveCardDownAsync(card);
        }
    }

    private void OnToggleFlagClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DialogueCardViewModel card && ViewModel != null)
        {
            _ = ViewModel.ToggleFlagAsync(card);
        }
    }

    private void OnDeleteCardClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DialogueCardViewModel card && ViewModel != null)
        {
            _ = ViewModel.ConfirmAndDeleteCardAsync(card);
        }
    }

    private void OnAiTranslateClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DialogueCardViewModel card && ViewModel != null)
        {
            _ = ViewModel.TranslateCardAsync(card);
        }
    }

    private void OnAcceptAiTranslationClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DialogueCardViewModel card && ViewModel != null)
        {
            _ = ViewModel.AcceptAiTranslationAsync(card);
        }
    }

    private void OnRejectAiTranslationClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DialogueCardViewModel card && ViewModel != null)
        {
            _ = ViewModel.RejectAiTranslationAsync(card);
        }
    }

    private void OnRevertToManualClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DialogueCardViewModel card && ViewModel != null)
        {
            _ = ViewModel.RevertToManualTranslationAsync(card);
        }
    }

    private void OnArabicTextBoxGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DialogueCardViewModel card && ViewModel != null)
        {
            ViewModel.ActiveEditingCard = card;
        }
    }

    private void OnCardTextLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DialogueCardViewModel card && ViewModel != null)
        {
            _ = ViewModel.SaveCardEditsAsync(card);
        }
    }

    private void OnArabicTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var isCtrlDown = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (e.Key == VirtualKey.Escape || (e.Key == VirtualKey.Enter && isCtrlDown))
        {
            // Commit and exit active editing mode
            ViewModel.StopEditing();
            DialogueCardsListView.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    private void OnFeedKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel == null || ViewModel.IsEditingActive) return;

        var isCtrlDown = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var isAltDown = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (e.Key == VirtualKey.Enter)
        {
            ViewModel.StartEditingSelected();
            e.Handled = true;
        }
        else if (isCtrlDown && e.Key == VirtualKey.F)
        {
            _ = ViewModel.ToggleFlagSelectedAsync();
            e.Handled = true;
        }
        else if (isAltDown && e.Key == VirtualKey.Up && ViewModel.SelectedCard != null)
        {
            _ = ViewModel.MoveCardUpAsync(ViewModel.SelectedCard);
            e.Handled = true;
        }
        else if (isAltDown && e.Key == VirtualKey.Down && ViewModel.SelectedCard != null)
        {
            _ = ViewModel.MoveCardDownAsync(ViewModel.SelectedCard);
            e.Handled = true;
        }
    }
}
