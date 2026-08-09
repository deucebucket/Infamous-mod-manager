using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfamousModManager.Services;

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

    [ObservableProperty]
    private string? _psarcExtractionStatus;

    public bool HasGameFolderStatus => !string.IsNullOrWhiteSpace(GameFolderStatus);

    public bool HasPsarcExtractionStatus => !string.IsNullOrWhiteSpace(PsarcExtractionStatus);

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

    [RelayCommand]
    private async Task UnpackGameFilesAsync()
    {
        try
        {
            var service = new GamePsarcExtractionService();
            var extractedFiles = await service.ExtractInstallArchivesAsync(GameFolderPath, GameFolderName);
            PsarcExtractionStatus = $"Extracted {extractedFiles} files. Original archives were moved to backup.";
        }
        catch (Exception exception)
        {
            PsarcExtractionStatus = $"Extraction failed: {exception.Message}";
        }
    }

    partial void OnGameFolderStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasGameFolderStatus));
    }

    partial void OnPsarcExtractionStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPsarcExtractionStatus));
    }
}
