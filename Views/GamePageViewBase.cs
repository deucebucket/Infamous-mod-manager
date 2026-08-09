using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using InfamousModManager.ViewModels;

namespace InfamousModManager.Views;

public abstract class GamePageViewBase : UserControl
{
    protected async void BrowseGameFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GamePageViewModelBase viewModel ||
            TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        await viewModel.BrowseGameFolderCommand.ExecuteAsync(topLevel.StorageProvider);
    }
}
