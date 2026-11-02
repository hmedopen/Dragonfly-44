using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Presentation.Input;
using Dragonfly44.Presentation.ViewModels;
using Dragonfly44.Presentation.Windowing;
using Microsoft.UI.Xaml;

namespace Dragonfly44.Presentation;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    public IWindowController WindowController { get; }

    public MainWindow(
        MainViewModel viewModel,
        IWindowController windowController,
        InputRouter inputRouter,
        IDialogService dialogService,
        IKeyboardShortcutService shortcutService,
        IFocusService focusService,
        IDragDropService dragDropService,
        IThemeService themeService,
        IWindowStateService windowStateService)
    {
        ViewModel = viewModel;
        WindowController = windowController;

        var tracePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Dragonfly44", "trace.txt");
        System.IO.File.AppendAllText(tracePath, "MainWindow constructor starting InitializeComponent...\n");

        try
        {
            InitializeComponent();
            System.IO.File.AppendAllText(tracePath, "MainWindow InitializeComponent succeeded!\n");
        }
        catch (System.Exception ex)
        {
            System.IO.File.AppendAllText(tracePath, $"MainWindow InitializeComponent failed: {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException}\n");
            throw;
        }

        RootGrid.DataContext = ViewModel;

        // Window infrastructure initialization
        TitleBarControl.WindowController = WindowController;
        WindowController.Attach(this, TitleBarControl.DragRegion);

        // Restore saved window placement from settings
        var savedBounds = windowStateService.LoadStateAsync().GetAwaiter().GetResult();
        WindowController.RestorePlacement(savedBounds);

        // Apply and register theme with visual root
        void ApplyTheme(ApplicationThemePreference theme)
        {
            RootGrid.RequestedTheme = theme switch
            {
                ApplicationThemePreference.Dark => ElementTheme.Dark,
                ApplicationThemePreference.Light => ElementTheme.Light,
                _ => ElementTheme.Default
            };
        }

        ApplyTheme(themeService.CurrentTheme);
        themeService.ThemeChanged += (s, theme) => ApplyTheme(theme);

        // Input & Interaction routing
        inputRouter.Attach(RootGrid);

        if (focusService is FocusService fs)
        {
            fs.RegisterRoot(RootGrid);
        }

        if (dragDropService is DragDropCoordinator ddc)
        {
            ddc.RegisterDropTarget(RootGrid);
        }

        // Connect dropped folder/file events to ViewModel
        dragDropService.FilesDropped += async (s, e) =>
        {
            if (e.FilePaths.Count > 0)
            {
                await ViewModel.HandleDroppedPathAsync(e.FilePaths[0]);
            }
        };

        // Dialog Presentation service registration
        if (dialogService is DialogService ds)
        {
            ds.RegisterPresenter(DialogHostControl);
        }

        // Register sample shortcuts
        shortcutService.Register(new KeyboardShortcut(
            Id: "Shortcut.Notification",
            Key: "N",
            Modifiers: KeyModifiers.Control,
            Action: () => ViewModel.TriggerTestNotification(),
            Description: "Trigger in-app notification",
            AllowDuringTextEditing: false));

        shortcutService.Register(new KeyboardShortcut(
            Id: "Shortcut.Dialog",
            Key: "D",
            Modifiers: KeyModifiers.Control,
            Action: () => _ = ViewModel.TriggerTestDialogAsync(),
            Description: "Trigger modal dialog",
            AllowDuringTextEditing: false));

        shortcutService.Register(new KeyboardShortcut(
            Id: "Shortcut.ToggleMaximize",
            Key: "F11",
            Modifiers: KeyModifiers.None,
            Action: () => WindowController.ToggleMaximize(),
            Description: "Toggle window maximize / restore",
            AllowDuringTextEditing: true,
            Priority: 10));
    }

    public async Task<string?> PickFolderAsync()
    {
        try
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
            folderPicker.FileTypeFilter.Add("*");
            var folder = await folderPicker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch
        {
            return null;
        }
    }

    // -------------------------------------------------------------
    // NAVIGATION HANDLERS
    // -------------------------------------------------------------

    private void OnNavProjectsClicked(object sender, RoutedEventArgs e) => ViewModel.NavigateToProjects();
    private void OnNavDraftsClicked(object sender, RoutedEventArgs e) => ViewModel.NavigateToDrafts();
    private void OnNavBackClicked(object sender, RoutedEventArgs e) => ViewModel.NavigateBack();

    // -------------------------------------------------------------
    // PROJECT HANDLERS
    // -------------------------------------------------------------

    private void OnStartCreateProjectClicked(object sender, RoutedEventArgs e) => ViewModel.IsCreatingProject = true;
    private void OnCancelCreateProjectClicked(object sender, RoutedEventArgs e) => ViewModel.IsCreatingProject = false;
    private void OnConfirmCreateProjectClicked(object sender, RoutedEventArgs e) => _ = ViewModel.CreateProjectAsync();

    private void OnOpenProjectClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ProjectItemViewModel vm)
        {
            _ = ViewModel.OpenProjectAsync(vm);
        }
    }

    private void OnStartRenameProjectClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ProjectItemViewModel vm)
        {
            vm.EditName = vm.Name;
            vm.IsEditing = true;
        }
    }

    private void OnConfirmRenameProjectClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ProjectItemViewModel vm)
        {
            var newName = vm.EditName;
            vm.IsEditing = false;
            _ = ViewModel.RenameProjectAsync(vm, newName);
        }
    }

    private void OnCancelRenameProjectClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ProjectItemViewModel vm)
        {
            vm.IsEditing = false;
        }
    }

    private void OnDeleteProjectClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ProjectItemViewModel vm)
        {
            _ = ViewModel.DeleteProjectAsync(vm);
        }
    }

    // -------------------------------------------------------------
    // CHAPTER HANDLERS
    // -------------------------------------------------------------

    private void OnStartCreateChapterClicked(object sender, RoutedEventArgs e) => ViewModel.IsCreatingChapter = true;
    private void OnCancelCreateChapterClicked(object sender, RoutedEventArgs e) => ViewModel.IsCreatingChapter = false;
    private void OnConfirmCreateChapterClicked(object sender, RoutedEventArgs e) => _ = ViewModel.CreateChapterAsync();

    private void OnOpenChapterClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChapterItemViewModel vm)
        {
            _ = ViewModel.OpenChapterAsync(vm);
        }
    }

    private void OnStartRenameChapterClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChapterItemViewModel vm)
        {
            vm.EditName = vm.Title;
            vm.IsEditing = true;
        }
    }

    private void OnConfirmRenameChapterClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChapterItemViewModel vm)
        {
            var newTitle = vm.EditName;
            vm.IsEditing = false;
            _ = ViewModel.RenameChapterAsync(vm, newTitle);
        }
    }

    private void OnCancelRenameChapterClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChapterItemViewModel vm)
        {
            vm.IsEditing = false;
        }
    }

    private void OnDeleteChapterClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChapterItemViewModel vm)
        {
            _ = ViewModel.DeleteChapterAsync(vm);
        }
    }

    // -------------------------------------------------------------
    // INGESTION HANDLERS
    // -------------------------------------------------------------

    private async void OnBrowseChapterFolderClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (!string.IsNullOrEmpty(path))
        {
            ViewModel.IngestFolderPath = path;
        }
    }

    private void OnImportChapterFolderClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.IngestChapterFolderAsync();
    }

    private void OnCancelIngestionClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelIngestion();
    }

    // -------------------------------------------------------------
    // DRAFT HANDLERS
    // -------------------------------------------------------------

    private void OnStartCreateDraftClicked(object sender, RoutedEventArgs e) => ViewModel.IsCreatingDraft = true;
    private void OnCancelCreateDraftClicked(object sender, RoutedEventArgs e) => ViewModel.IsCreatingDraft = false;
    private void OnConfirmCreateDraftClicked(object sender, RoutedEventArgs e) => _ = ViewModel.CreateDraftAsync();

    private void OnOpenDraftClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DraftItemViewModel vm)
        {
            _ = ViewModel.OpenDraftAsync(vm);
        }
    }

    private void OnStartRenameDraftClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DraftItemViewModel vm)
        {
            vm.EditName = vm.Name;
            vm.IsEditing = true;
        }
    }

    private void OnConfirmRenameDraftClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DraftItemViewModel vm)
        {
            var newName = vm.EditName;
            vm.IsEditing = false;
            _ = ViewModel.RenameDraftAsync(vm, newName);
        }
    }

    private void OnCancelRenameDraftClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DraftItemViewModel vm)
        {
            vm.IsEditing = false;
        }
    }

    private void OnDeleteDraftClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DraftItemViewModel vm)
        {
            _ = ViewModel.DeleteDraftAsync(vm);
        }
    }

    private async void OnBrowseDraftFolderClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (!string.IsNullOrEmpty(path))
        {
            ViewModel.IngestFolderPath = path;
        }
    }

    private void OnImportDraftFolderClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.IngestDraftFolderAsync();
    }

    // -------------------------------------------------------------
    // MANGA CANVAS HANDLERS
    // -------------------------------------------------------------

    private void OnCloseCanvasClicked(object sender, RoutedEventArgs e) => ViewModel.NavigateBack();
    private void OnToggleCanvasModeClicked(object sender, RoutedEventArgs e) => ViewModel.CanvasViewModel.ToggleViewMode();
    private void OnCanvasPrevPageClicked(object sender, RoutedEventArgs e) => ViewModel.CanvasViewModel.PreviousPage();
    private void OnCanvasNextPageClicked(object sender, RoutedEventArgs e) => ViewModel.CanvasViewModel.NextPage();
    private void OnCanvasZoomInClicked(object sender, RoutedEventArgs e) => ViewModel.CanvasViewModel.ZoomIn();
    private void OnCanvasZoomOutClicked(object sender, RoutedEventArgs e) => ViewModel.CanvasViewModel.ZoomOut();
    private void OnCanvasFitPageClicked(object sender, RoutedEventArgs e) => ViewModel.CanvasViewModel.FitPage();
    private void OnCanvasFitWidthClicked(object sender, RoutedEventArgs e) => ViewModel.CanvasViewModel.FitWidth();
    private void OnCanvasResetZoomClicked(object sender, RoutedEventArgs e) => ViewModel.CanvasViewModel.ResetZoom();

    private void OnOpenChapterCanvasClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.OpenChapterCanvasAsync(0);
    }

    private void OnOpenDraftCanvasClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.OpenDraftCanvasAsync(0);
    }

    private void OnOpenPageInCanvasClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PageItemViewModel pvm)
        {
            var pageIndex = Math.Max(0, pvm.PageNumber - 1);
            _ = ViewModel.OpenChapterCanvasAsync(pageIndex);
        }
    }

    private void OnOpenDraftPageInCanvasClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DraftPageItemViewModel dpvm)
        {
            var pageIndex = Math.Max(0, dpvm.PageNumber - 1);
            _ = ViewModel.OpenDraftCanvasAsync(pageIndex);
        }
    }

    private void OnToggleDialogueFeedClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleDialogueFeed();
    }

    // -------------------------------------------------------------
    // AI SETTINGS MODAL HANDLERS
    // -------------------------------------------------------------

    private void OnOpenAiSettingsClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenAiSettings();
    }

    private void OnCloseAiSettingsClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseAiSettings();
    }

    private void OnDeleteApiKeyClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.AiSettingsViewModel.DeleteApiKeyAsync();
    }

    private void OnTestConnectionClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.AiSettingsViewModel.TestConnectionAsync();
    }

    private async void OnSaveAiSettingsClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.AiSettingsViewModel.SaveSettingsAsync();
        ViewModel.CloseAiSettings();
    }

    // -------------------------------------------------------------
    // DIAGNOSTIC HANDLERS
    // -------------------------------------------------------------

    private void OnTriggerNotificationClicked(object sender, RoutedEventArgs e) => ViewModel.TriggerTestNotification();
    private void OnTriggerDialogClicked(object sender, RoutedEventArgs e) => _ = ViewModel.TriggerTestDialogAsync();
}
