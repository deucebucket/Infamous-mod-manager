using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InfamousModManager.ViewModels;

public abstract partial class GamePageViewModelBase : ViewModelBase
{
    protected GamePageViewModelBase(string gameFolderName)
    {
        GameFolderName = gameFolderName;
    }

    public string GameFolderName { get; }

    [ObservableProperty]
    private string _gameFolderPath = string.Empty;

    [ObservableProperty]
    private string? _gameFolderStatus;

    public bool HasGameFolderStatus => !string.IsNullOrWhiteSpace(GameFolderStatus);

    [RelayCommand]
    private async Task BrowseGameFolderAsync(IStorageProvider storageProvider)
    {
        var selectedFolders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the RPCS3 dev_hdd0/game folder",
            AllowMultiple = false
        });

        if (selectedFolders.Count == 0)
        {
            return;
        }

        GameFolderPath = selectedFolders[0].Path.LocalPath;
        ValidateGameFolder();
    }

    private void ValidateGameFolder()
    {
        var normalizedPath = GameFolderPath.Replace('\\', '/').TrimEnd('/');
        const string rpcS3GamePath = "/rpcs3/dev_hdd0/game";

        if (!normalizedPath.Contains(rpcS3GamePath, StringComparison.OrdinalIgnoreCase))
        {
            GameFolderStatus = "Select the RPCS3 dev_hdd0/game folder.";
            return;
        }

        var requiredGameFolder = Path.Combine(GameFolderPath, GameFolderName);
        GameFolderStatus = Directory.Exists(requiredGameFolder)
            ? $"Game folder found: {requiredGameFolder}"
            : $"Game folder '{GameFolderName}' was not found in the selected folder.";
    }

    partial void OnGameFolderStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasGameFolderStatus));
    }
}
