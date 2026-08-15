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
    private UserDataStore? _userDataStore;

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
    private bool _isGameFolderValid;

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

    [ObservableProperty]
    private string _xppArchivePath = string.Empty;

    [ObservableProperty]
    private string _xppExtractionOutputPath = string.Empty;

    [ObservableProperty]
    private string? _xppExtractionStatus;

    [ObservableProperty]
    private bool _isXppExtractionSuccessful;

    public bool HasGameFolderStatus => !string.IsNullOrWhiteSpace(GameFolderStatus);

    public bool HasPsarcExtractionStatus => !string.IsNullOrWhiteSpace(PsarcExtractionStatus);

    public bool IsPsarcExtractionFailed => HasPsarcExtractionStatus && !IsPsarcExtractionSuccessful;

    public bool HasModInstallationStatus => !string.IsNullOrWhiteSpace(ModInstallationStatus);

    public bool IsModInstallationFailed => HasModInstallationStatus && !IsModInstallationSuccessful;

    public bool HasXppExtractionStatus => !string.IsNullOrWhiteSpace(XppExtractionStatus);

    public bool IsXppExtractionFailed => HasXppExtractionStatus && !IsXppExtractionSuccessful;

    public void LoadSavedData(UserDataStore userDataStore)
    {
        _userDataStore = userDataStore;
        var savedGameFolder = userDataStore.GetGameFolder(GameFolderName);

        if (savedGameFolder is not null)
        {
            GameFolderPath = savedGameFolder.GameFolderPath;
        }
    }

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
    }

    private void ValidateGameFolder()
    {
        if (string.IsNullOrWhiteSpace(GameFolderPath))
        {
            IsGameFolderValid = false;
            GameFolderStatus = null;
            SaveGameFolderData();
            return;
        }

        var normalizedPath = GameFolderPath.Replace('\\', '/').TrimEnd('/');
        const string rpcS3GamePath = "/rpcs3/dev_hdd0/game";

        if (!normalizedPath.Contains(rpcS3GamePath, StringComparison.OrdinalIgnoreCase))
        {
            IsGameFolderValid = false;
            GameFolderStatus = "Select the RPCS3 dev_hdd0/game folder.";
            SaveGameFolderData();
            return;
        }

        var requiredGameFolder = Path.Combine(GameFolderPath, GameFolderName);
        IsGameFolderValid = Directory.Exists(requiredGameFolder);
        GameFolderStatus = IsGameFolderValid
            ? $"Game folder found: {requiredGameFolder}"
            : $"Game folder '{GameFolderName}' was not found in the selected folder.";
        SaveGameFolderData();
    }

    private void SaveGameFolderData() =>
        _userDataStore?.SaveGameFolder(GameFolderName, GameFolderPath, IsGameFolderValid);

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
    private async Task BrowseXppArchiveAsync(IStorageProvider storageProvider)
    {
        var selectedFiles = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select an XPP or XPPS archive",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("XPP/XPPS archives") { Patterns = ["*.xpp", "*.xpps"] }
            ]
        });

        if (selectedFiles.Count > 0)
        {
            XppArchivePath = selectedFiles[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task BrowseXppExtractionOutputAsync(IStorageProvider storageProvider)
    {
        var selectedFolders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select an extraction output folder",
            AllowMultiple = false
        });

        if (selectedFolders.Count > 0)
        {
            XppExtractionOutputPath = selectedFolders[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task ExtractXppArchiveAsync()
    {
        XppExtractionStatus = null;
        IsXppExtractionSuccessful = false;

        try
        {
            var service = new XppArchiveExtractionService();
            var result = await Task.Run(() => service.Extract(XppArchivePath, XppExtractionOutputPath));
            IsXppExtractionSuccessful = true;
            XppExtractionStatus = result.IsEmptyStub
                ? $"Empty archive extracted to: {result.OutputDirectory}"
                : $"Extracted {result.ExtractedResourceCount} resources, {result.MetadataChunkCount} metadata chunks, and {result.TextureCount} DDS textures to: {result.OutputDirectory}";
        }
        catch (Exception exception)
        {
            XppExtractionStatus = $"Archive extraction failed: {exception.Message}";
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
            var installationHistory = _userDataStore?.GetModInstallationHistory(GameFolderName) ?? [];
            var result = await Task.Run(() => service.Install(GameFolderPath, GameFolderName, ModFilePath, installationHistory));
            _userDataStore?.SaveModInstallation(GameFolderName, result.HistoryEntry);
            IsModInstallationSuccessful = true;
            ModInstallationStatus = $"Mod installed: {result.InstalledPath}";
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

    partial void OnGameFolderPathChanged(string value)
    {
        ValidateGameFolder();
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

    partial void OnXppExtractionStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasXppExtractionStatus));
        OnPropertyChanged(nameof(IsXppExtractionFailed));
    }

    partial void OnIsXppExtractionSuccessfulChanged(bool value)
    {
        OnPropertyChanged(nameof(IsXppExtractionFailed));
    }
}
