using Dragonfly44.Presentation.ViewModels;
using Microsoft.UI.Xaml;

namespace Dragonfly44.Presentation;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        if (Content is FrameworkElement root)
        {
            root.DataContext = ViewModel;
        }
    }
}
