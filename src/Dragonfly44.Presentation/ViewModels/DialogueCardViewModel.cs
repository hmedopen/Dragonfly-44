using Dragonfly44.Application.Common.TypeR;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Dragonfly44.Presentation.ViewModels;

/// <summary>
/// Presentation model representing an individual dialogue card in the translation feed.
/// Supports TypeR prefix isolation, Arabic RTL text editing, flagging, selection, reordering,
/// AI translation preview, explicit acceptance workflow, and manual translation preservation.
/// </summary>
public class DialogueCardViewModel : ViewModelBase
{
    private int _displayNumber;
    private string _rawText = string.Empty;
    private string _translatedText = string.Empty;
    private BubbleShapeType _shapeType = BubbleShapeType.StandardSpeech;
    private bool _isSelected;
    private bool _isEditing;
    private bool _isFlagged;
    private int _readingOrderIndex;

    // Phase 7 AI Translation & Data Integrity State
    private bool _isTranslating;
    private string? _pendingAiTranslation;
    private string? _pendingAiNotes;
    private Guid? _pendingHistoryEntryId;
    private bool _isAiTranslated;
    private string? _lastManualTranslation;
    private string? _translationError;

    public Guid Id { get; }
    public Guid PageId { get; }
    public int PageNumber { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public int DisplayNumber
    {
        get => _displayNumber;
        set
        {
            if (SetProperty(ref _displayNumber, value))
            {
                OnPropertyChanged(nameof(DisplayNumberText));
            }
        }
    }

    public string DisplayNumberText => $"#{DisplayNumber}";
    public string PageBadgeText => $"P{PageNumber}";

    public string RawText
    {
        get => _rawText;
        set
        {
            if (SetProperty(ref _rawText, value))
            {
                OnPropertyChanged(nameof(CanTranslateWithAi));
            }
        }
    }

    public string TranslatedText
    {
        get => _translatedText;
        set => SetProperty(ref _translatedText, value);
    }

    public BubbleShapeType ShapeType
    {
        get => _shapeType;
        set
        {
            if (SetProperty(ref _shapeType, value))
            {
                OnPropertyChanged(nameof(TypeRPrefix));
                OnPropertyChanged(nameof(ClassificationDisplayText));
                OnPropertyChanged(nameof(ClassificationIndex));
            }
        }
    }

    public int ClassificationIndex
    {
        get => (int)ShapeType;
        set => ShapeType = (BubbleShapeType)value;
    }

    public string TypeRPrefix => TypeRPrefixHandler.GetPrefixForShapeType(ShapeType);

    public string ClassificationDisplayText => ShapeType switch
    {
        BubbleShapeType.StandardSpeech => "Speech",
        BubbleShapeType.Shout => "Shout",
        BubbleShapeType.Whisper => "Whisper",
        BubbleShapeType.Thought => "Thought",
        BubbleShapeType.NarrativeBox => "Narration",
        BubbleShapeType.SoundEffect => "SFX",
        _ => "Speech"
    };

    public int ReadingOrderIndex
    {
        get => _readingOrderIndex;
        set => SetProperty(ref _readingOrderIndex, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(CardBorderBrush));
                OnPropertyChanged(nameof(CardBorderThickness));
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(CardBorderBrush));
                OnPropertyChanged(nameof(CardBorderThickness));
            }
        }
    }

    public bool IsFlagged
    {
        get => _isFlagged;
        set
        {
            if (SetProperty(ref _isFlagged, value))
            {
                OnPropertyChanged(nameof(FlagIcon));
                OnPropertyChanged(nameof(FlagBrush));
                OnPropertyChanged(nameof(CardBorderBrush));
            }
        }
    }

    public Brush CardBorderBrush
    {
        get
        {
            var app = Microsoft.UI.Xaml.Application.Current;
            if (app == null) return null!;
            if (IsEditing) return (Brush)app.Resources["DragonflyPlaceholderSuccessBrush"];
            if (IsSelected) return (Brush)app.Resources["DragonflyPlaceholderAccentBrush"];
            if (IsFlagged) return (Brush)app.Resources["DragonflyPlaceholderWarningBrush"];
            return (Brush)app.Resources["DragonflyPlaceholderBorderBrush"];
        }
    }

    public Thickness CardBorderThickness => (IsSelected || IsEditing) ? new Thickness(2) : new Thickness(1);

    public Brush FlagBrush
    {
        get
        {
            var app = Microsoft.UI.Xaml.Application.Current;
            if (app == null) return null!;
            return IsFlagged
                ? (Brush)app.Resources["DragonflyPlaceholderWarningBrush"]
                : (Brush)app.Resources["DragonflyPlaceholderTextMutedBrush"];
        }
    }

    public string FlagIcon => IsFlagged ? "🚩" : "⚐";

    // ------------------------------------------------------------------------
    // Phase 7 AI Translation Properties
    // ------------------------------------------------------------------------

    public bool IsTranslating
    {
        get => _isTranslating;
        set
        {
            if (SetProperty(ref _isTranslating, value))
            {
                OnPropertyChanged(nameof(CanTranslateWithAi));
                OnPropertyChanged(nameof(TranslatingStatusText));
            }
        }
    }

    public bool CanTranslateWithAi => !IsTranslating && !string.IsNullOrWhiteSpace(RawText);

    public string TranslatingStatusText => IsTranslating ? "Translating..." : "⚡ Translate AI";

    public string? PendingAiTranslation
    {
        get => _pendingAiTranslation;
        private set
        {
            if (SetProperty(ref _pendingAiTranslation, value))
            {
                OnPropertyChanged(nameof(HasPendingAiTranslation));
                OnPropertyChanged(nameof(HasPendingAiTranslationVisibility));
            }
        }
    }

    public string? PendingAiNotes
    {
        get => _pendingAiNotes;
        private set
        {
            if (SetProperty(ref _pendingAiNotes, value))
            {
                OnPropertyChanged(nameof(HasPendingAiNotes));
                OnPropertyChanged(nameof(HasPendingAiNotesVisibility));
            }
        }
    }

    public Guid? PendingHistoryEntryId
    {
        get => _pendingHistoryEntryId;
        private set => SetProperty(ref _pendingHistoryEntryId, value);
    }

    public bool HasPendingAiTranslation => !string.IsNullOrWhiteSpace(PendingAiTranslation);
    public bool HasPendingAiNotes => !string.IsNullOrWhiteSpace(PendingAiNotes);

    public Visibility HasPendingAiTranslationVisibility => HasPendingAiTranslation ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasPendingAiNotesVisibility => HasPendingAiNotes ? Visibility.Visible : Visibility.Collapsed;

    public bool IsAiTranslated
    {
        get => _isAiTranslated;
        set
        {
            if (SetProperty(ref _isAiTranslated, value))
            {
                OnPropertyChanged(nameof(CanRevertToManual));
                OnPropertyChanged(nameof(CanRevertToManualVisibility));
                OnPropertyChanged(nameof(AiBadgeText));
                OnPropertyChanged(nameof(IsAiBadgeVisible));
            }
        }
    }

    public string? LastManualTranslation
    {
        get => _lastManualTranslation;
        set
        {
            if (SetProperty(ref _lastManualTranslation, value))
            {
                OnPropertyChanged(nameof(CanRevertToManual));
                OnPropertyChanged(nameof(CanRevertToManualVisibility));
            }
        }
    }

    public bool CanRevertToManual => IsAiTranslated && !string.IsNullOrWhiteSpace(LastManualTranslation);
    public Visibility CanRevertToManualVisibility => CanRevertToManual ? Visibility.Visible : Visibility.Collapsed;

    public string AiBadgeText => IsAiTranslated ? "🤖 AI" : string.Empty;
    public Visibility IsAiBadgeVisible => IsAiTranslated ? Visibility.Visible : Visibility.Collapsed;

    public string? TranslationError
    {
        get => _translationError;
        set
        {
            if (SetProperty(ref _translationError, value))
            {
                OnPropertyChanged(nameof(HasTranslationError));
                OnPropertyChanged(nameof(HasTranslationErrorVisibility));
            }
        }
    }

    public bool HasTranslationError => !string.IsNullOrWhiteSpace(TranslationError);
    public Visibility HasTranslationErrorVisibility => HasTranslationError ? Visibility.Visible : Visibility.Collapsed;

    // Actions
    public event EventHandler? RequestSave;
    public event EventHandler? RequestDelete;
    public event EventHandler? RequestToggleFlag;
    public event EventHandler? RequestSelect;
    public event EventHandler? RequestMoveUp;
    public event EventHandler? RequestMoveDown;
    public event EventHandler? RequestAiTranslate;
    public event EventHandler? RequestAcceptAiTranslation;
    public event EventHandler? RequestRejectAiTranslation;
    public event EventHandler? RequestRevertToManual;

    public DialogueCardViewModel(Bubble bubble, int pageNumber, int displayNumber)
    {
        Id = bubble.Id;
        PageId = bubble.PageId;
        PageNumber = pageNumber;
        DisplayNumber = displayNumber;
        RawText = bubble.RawText;
        TranslatedText = bubble.TranslatedText;
        ShapeType = bubble.ShapeType;
        ReadingOrderIndex = bubble.ReadingOrderIndex;
        IsFlagged = bubble.IsFlagged;
        IsAiTranslated = bubble.IsAiTranslated;
        LastManualTranslation = bubble.LastManualTranslation;
        X = bubble.X;
        Y = bubble.Y;
        Width = bubble.Width;
        Height = bubble.Height;
    }

    public void TriggerSave() => RequestSave?.Invoke(this, EventArgs.Empty);
    public void TriggerDelete() => RequestDelete?.Invoke(this, EventArgs.Empty);
    public void TriggerToggleFlag() => RequestToggleFlag?.Invoke(this, EventArgs.Empty);
    public void TriggerSelect() => RequestSelect?.Invoke(this, EventArgs.Empty);
    public void TriggerMoveUp() => RequestMoveUp?.Invoke(this, EventArgs.Empty);
    public void TriggerMoveDown() => RequestMoveDown?.Invoke(this, EventArgs.Empty);
    public void TriggerAiTranslate() => RequestAiTranslate?.Invoke(this, EventArgs.Empty);
    public void TriggerAcceptAiTranslation() => RequestAcceptAiTranslation?.Invoke(this, EventArgs.Empty);
    public void TriggerRejectAiTranslation() => RequestRejectAiTranslation?.Invoke(this, EventArgs.Empty);
    public void TriggerRevertToManual() => RequestRevertToManual?.Invoke(this, EventArgs.Empty);

    public void SetPendingTranslation(string translation, string? notes, Guid? historyEntryId)
    {
        PendingAiTranslation = translation;
        PendingAiNotes = notes;
        PendingHistoryEntryId = historyEntryId;
        TranslationError = null;
        IsTranslating = false;
    }

    public void ClearPendingTranslation()
    {
        PendingAiTranslation = null;
        PendingAiNotes = null;
        PendingHistoryEntryId = null;
    }

    public void ApplyAcceptedTranslation(string translation)
    {
        if (!IsAiTranslated && !string.IsNullOrWhiteSpace(TranslatedText))
        {
            LastManualTranslation = TranslatedText;
        }

        TranslatedText = translation;
        IsAiTranslated = true;
        ClearPendingTranslation();
        TriggerSave();
    }

    public void ApplyRevertedTranslation(string manualText)
    {
        TranslatedText = manualText;
        IsAiTranslated = false;
        LastManualTranslation = null;
        ClearPendingTranslation();
        TriggerSave();
    }
}
