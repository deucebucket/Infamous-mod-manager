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

    [ObservableProperty]
    private bool _isPsarcExtractionSuccessful;

    [ObservableProperty]
    private string _modFilePath = string.Empty;

    [ObservableProperty]
    private string? _modInstallationStatus;

    [ObservableProperty]
    private bool _isModInstallationSuccessful;

    public bool HasGameFolderStatus => !string.IsNullOrWhiteSpace(GameFolderStatus);

    public bool HasPsarcExtractionStatus => !string.IsNullOrWhiteSpace(PsarcExtractionStatus);

    public bool IsPsarcExtractionFailed => HasPsarcExtractionStatus && !IsPsarcExtractionSuccessful;

    public bool HasModInstallationStatus => !string.IsNullOrWhiteSpace(ModInstallationStatus);

    public bool IsModInstallationFailed => HasModInstallationStatus && !IsModInstallationSuccessful;

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
        PsarcExtractionStatus = null;
        IsPsarcExtractionSuccessful = false;

        try
        {
            var service = new GamePsarcExtractionService();
            var extractedFiles = await service.ExtractInstallArchivesAsync(GameFolderPath, GameFolderName);
            IsPsarcExtractionSuccessful = true;
            PsarcExtractionStatus = $"Extracted {extractedFiles} files. Original archives were moved to backup.";
        }
        catch (Exception exception)
        {
            IsPsarcExtractionSuccessful = false;
            PsarcExtractionStatus = $"Extraction failed: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task BrowseModFileAsync(IStorageProvider storageProvider)
    {
        var selectedFiles = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select an XPPS mod file",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("XPPS files") { Patterns = ["*.xpps"] }]
        });

        if (selectedFiles.Count > 0)
        {
            ModFilePath = selectedFiles[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task InstallModAsync()
    {
        ModInstallationStatus = null;
        IsModInstallationSuccessful = false;

        try
        {
            var service = new GameModInstallationService();
            var installedPath = await Task.Run(() => service.Install(GameFolderPath, GameFolderName, ModFilePath));
            IsModInstallationSuccessful = true;
            ModInstallationStatus = $"Mod installed: {installedPath}";
        }
        catch (Exception exception)
        {
            ModInstallationStatus = $"Mod installation failed: {exception.Message}";
        }
    }

    partial void OnGameFolderStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasGameFolderStatus));
    }

    partial void OnPsarcExtractionStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPsarcExtractionStatus));
        OnPropertyChanged(nameof(IsPsarcExtractionFailed));
    }

    partial void OnIsPsarcExtractionSuccessfulChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPsarcExtractionFailed));
    }

    partial void OnModInstallationStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasModInstallationStatus));
        OnPropertyChanged(nameof(IsModInstallationFailed));
    }

    partial void OnIsModInstallationSuccessfulChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModInstallationFailed));
    }
}
