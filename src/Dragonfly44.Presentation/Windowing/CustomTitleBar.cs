using Dragonfly44.Presentation.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dragonfly44.Presentation.Windowing;

public class CustomTitleBar : ContentControl
{
    private CustomButton? _minimizeButton;
    private CustomButton? _maximizeButton;
    private CustomButton? _closeButton;
    private FrameworkElement? _dragRegion;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(CustomTitleBar),
            new PropertyMetadata("Dragonfly 44"));

    public static readonly DependencyProperty WindowControllerProperty =
        DependencyProperty.Register(
            nameof(WindowController),
            typeof(IWindowController),
            typeof(CustomTitleBar),
            new PropertyMetadata(null, OnWindowControllerChanged));

    public static readonly DependencyProperty IsMaximizedProperty =
        DependencyProperty.Register(
            nameof(IsMaximized),
            typeof(bool),
            typeof(CustomTitleBar),
            new PropertyMetadata(false));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IWindowController? WindowController
    {
        get => (IWindowController?)GetValue(WindowControllerProperty);
        set => SetValue(WindowControllerProperty, value);
    }

    public bool IsMaximized
    {
        get => (bool)GetValue(IsMaximizedProperty);
        set => SetValue(IsMaximizedProperty, value);
    }

    public FrameworkElement? DragRegion => _dragRegion;

    public CustomTitleBar()
    {
        DefaultStyleKey = typeof(CustomTitleBar);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_minimizeButton != null) _minimizeButton.Click -= OnMinimizeClicked;
        if (_maximizeButton != null) _maximizeButton.Click -= OnMaximizeClicked;
        if (_closeButton != null) _closeButton.Click -= OnCloseClicked;

        _minimizeButton = GetTemplateChild("PART_MinimizeButton") as CustomButton;
        _maximizeButton = GetTemplateChild("PART_MaximizeButton") as CustomButton;
        _closeButton = GetTemplateChild("PART_CloseButton") as CustomButton;
        _dragRegion = GetTemplateChild("PART_DragRegion") as FrameworkElement;

        if (_minimizeButton != null) _minimizeButton.Click += OnMinimizeClicked;
        if (_maximizeButton != null) _maximizeButton.Click += OnMaximizeClicked;
        if (_closeButton != null) _closeButton.Click += OnCloseClicked;

        if (_dragRegion != null && WindowController != null)
        {
            WindowController.SetDragRegion(_dragRegion);
        }

        UpdateVisualState();
    }

    private static void OnWindowControllerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CustomTitleBar titleBar && e.NewValue is IWindowController controller)
        {
            controller.StateChanged += (s, args) =>
            {
                titleBar.IsMaximized = controller.IsMaximized;
                titleBar.UpdateVisualState();
            };

            if (titleBar._dragRegion != null)
            {
                controller.SetDragRegion(titleBar._dragRegion);
            }
        }
    }

    private void OnMinimizeClicked(object sender, RoutedEventArgs e)
    {
        WindowController?.Minimize();
    }

    private void OnMaximizeClicked(object sender, RoutedEventArgs e)
    {
        WindowController?.ToggleMaximize();
        UpdateVisualState();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        WindowController?.Close();
    }

    private void UpdateVisualState()
    {
        if (_maximizeButton != null)
        {
            _maximizeButton.Content = IsMaximized ? "🗗" : "🗖";
        }
    }
}
