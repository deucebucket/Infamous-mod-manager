using Avalonia.Controls;
using Avalonia.Interactivity;

namespace InfamousModManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = Avalonia.Controls.WindowState.Minimized;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
