using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Presentation.Input;
using Dragonfly44.Presentation.Services;
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
        IDragDropService dragDropService)
    {
        InitializeComponent();
        ViewModel = viewModel;
        WindowController = windowController;

        RootGrid.DataContext = ViewModel;

        // Workstream 1: Window infrastructure initialization
        TitleBarControl.WindowController = WindowController;
        WindowController.Attach(this, TitleBarControl.DragRegion);

        // Workstream 5: Input & Interaction routing
        inputRouter.Attach(RootGrid);

        if (focusService is FocusService fs)
        {
            fs.RegisterRoot(RootGrid);
        }

        if (dragDropService is DragDropCoordinator ddc)
        {
            ddc.RegisterDropTarget(RootGrid);
        }

        // Workstream 6: Dialog Presentation service registration
        if (dialogService is DialogService ds)
        {
            ds.RegisterHost(DialogHostControl);
        }

        // Register sample shortcuts demonstrating global vs text-editing distinction
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
            AllowDuringTextEditing: true, // Function keys allowed during text editing
            Priority: 10));
    }

    private void OnTriggerNotificationClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.TriggerTestNotification();
    }

    private void OnTriggerDialogClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.TriggerTestDialogAsync();
    }
}
