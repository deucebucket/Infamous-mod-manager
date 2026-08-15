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

    protected async void BrowseModFile_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GamePageViewModelBase viewModel ||
            TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        await viewModel.BrowseModFileCommand.ExecuteAsync(topLevel.StorageProvider);
    }

    protected async void BrowseXppArchive_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GamePageViewModelBase viewModel ||
            TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        await viewModel.BrowseXppArchiveCommand.ExecuteAsync(topLevel.StorageProvider);
    }

    protected async void BrowseXppExtractionOutput_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GamePageViewModelBase viewModel ||
            TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        await viewModel.BrowseXppExtractionOutputCommand.ExecuteAsync(topLevel.StorageProvider);
    }
}
