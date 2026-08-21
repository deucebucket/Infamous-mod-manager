using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfamousModManager.Models;
using InfamousModManager.Services;

namespace InfamousModManager.ViewModels;

public abstract partial class GamePageViewModelBase : ViewModelBase
{
    private readonly IReadOnlyList<GameEdition> _gameEditions;
    private UserDataStore? _userDataStore;
    private GameEdition? _detectedEdition;

    protected GamePageViewModelBase(IReadOnlyList<GameEdition> gameEditions)
    {
        if (gameEditions.Count == 0)
        {
            throw new ArgumentException("At least one game edition is required.", nameof(gameEditions));
        }

        _gameEditions = gameEditions;
    }

    // The first ID remains the compatibility key for existing saved data.
    public string GameFolderName => _gameEditions[0].TitleId;

    public string ActiveGameFolderName => _detectedEdition?.TitleId ?? GameFolderName;

    public string? DetectedEditionName => _detectedEdition?.DisplayName;

    public bool IsLooseModInstallation => _detectedEdition?.ModMode == GameModMode.LooseXpps;

    public bool IsPackedPsarcInstallation => _detectedEdition?.ModMode == GameModMode.PackedPsarc;

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
    private string _psarcProfilePath = string.Empty;

    [ObservableProperty]
    private string _cleanRetailPsarcPath = string.Empty;

    [ObservableProperty]
    private string? _packedPsarcStatus;

    [ObservableProperty]
    private bool _isPackedPsarcSuccessful;

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

    public bool HasPackedPsarcStatus => !string.IsNullOrWhiteSpace(PackedPsarcStatus);

    public bool IsPackedPsarcFailed => HasPackedPsarcStatus && !IsPackedPsarcSuccessful;

    public bool HasXppExtractionStatus => !string.IsNullOrWhiteSpace(XppExtractionStatus);

    public bool IsXppExtractionFailed => HasXppExtractionStatus && !IsXppExtractionSuccessful;

    public void LoadSavedData(UserDataStore userDataStore)
    {
        _userDataStore = userDataStore;
        var savedGameFolder = _gameEditions
            .Select(edition => userDataStore.GetGameFolder(edition.TitleId))
            .FirstOrDefault(saved => saved is not null && !string.IsNullOrWhiteSpace(saved.GameFolderPath));

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
            SetDetectedEdition(null);
            IsGameFolderValid = false;
            GameFolderStatus = null;
            SaveGameFolderData();
            return;
        }

        var detection = GameInstallationDetector.Detect(GameFolderPath, _gameEditions);
        SetDetectedEdition(detection.Edition);
        IsGameFolderValid = detection.IsValid;
        GameFolderStatus = detection.Message;
        SaveGameFolderData();
    }

    private void SaveGameFolderData()
    {
        if (_userDataStore is not null && _detectedEdition is not null)
        {
            _userDataStore.SaveGameFolder(_detectedEdition.TitleId, GameFolderPath, IsGameFolderValid);
        }
    }

    private void SetDetectedEdition(GameEdition? edition)
    {
        if (Equals(_detectedEdition, edition))
        {
            return;
        }

        _detectedEdition = edition;
        OnPropertyChanged(nameof(ActiveGameFolderName));
        OnPropertyChanged(nameof(DetectedEditionName));
        OnPropertyChanged(nameof(IsLooseModInstallation));
        OnPropertyChanged(nameof(IsPackedPsarcInstallation));
    }

    [RelayCommand]
    private async Task UnpackGameFilesAsync()
    {
        PsarcExtractionStatus = null;
        IsPsarcExtractionSuccessful = false;

        try
        {
            if (!IsLooseModInstallation)
            {
                throw new InvalidOperationException("This edition streams packed PSARCs. Use the packed profile controls instead of unpacking it.");
            }

            var service = new GamePsarcExtractionService();
            var extractedFiles = await service.ExtractInstallArchivesAsync(GameFolderPath, ActiveGameFolderName);
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
    private async Task BrowsePsarcProfileAsync(IStorageProvider storageProvider)
    {
        var selectedFolders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select a verified PSARC profile folder",
            AllowMultiple = false
        });

        if (selectedFolders.Count > 0)
        {
            PsarcProfilePath = selectedFolders[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task BrowseCleanRetailPsarcAsync(IStorageProvider storageProvider)
    {
        var selectedFolders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the clean retail PSARC pair (optional after first backup)",
            AllowMultiple = false
        });

        if (selectedFolders.Count > 0)
        {
            CleanRetailPsarcPath = selectedFolders[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task InstallPackedPsarcProfileAsync()
    {
        PackedPsarcStatus = null;
        IsPackedPsarcSuccessful = false;
        try
        {
            if (!IsGameFolderValid || !IsPackedPsarcInstallation)
            {
                throw new InvalidOperationException("Select a valid packed-PSARC edition first.");
            }

            var service = new PackedPsarcProfileService();
            var result = await Task.Run(() => service.InstallProfile(
                GameFolderPath,
                ActiveGameFolderName,
                PsarcProfilePath,
                string.IsNullOrWhiteSpace(CleanRetailPsarcPath) ? null : CleanRetailPsarcPath));
            IsPackedPsarcSuccessful = true;
            PackedPsarcStatus = FormatPackedPsarcResult(result);
        }
        catch (Exception exception)
        {
            PackedPsarcStatus = $"Packed profile installation failed: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task RestoreRetailPsarcAsync()
    {
        PackedPsarcStatus = null;
        IsPackedPsarcSuccessful = false;
        try
        {
            if (!IsGameFolderValid || !IsPackedPsarcInstallation)
            {
                throw new InvalidOperationException("Select a valid packed-PSARC edition first.");
            }

            var service = new PackedPsarcProfileService();
            var result = await Task.Run(() => service.RestoreRetail(GameFolderPath, ActiveGameFolderName));
            IsPackedPsarcSuccessful = true;
            PackedPsarcStatus = FormatPackedPsarcResult(result);
        }
        catch (Exception exception)
        {
            PackedPsarcStatus = $"Retail restore failed: {exception.Message}";
        }
    }

    private static string FormatPackedPsarcResult(PackedPsarcOperationResult result) =>
        $"{result.Action}: {string.Join(", ", result.Archives.Select(archive => $"{archive.FileName} ({archive.Length:N0} bytes, {archive.EntryCount} entries, SHA-256 {archive.Sha256[..12]}…)") )}.";

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
            if (!IsLooseModInstallation)
            {
                throw new InvalidOperationException("This edition requires a verified packed PSARC profile, not a loose XPPS file.");
            }

            var service = new GameModInstallationService();
            var installationHistory = _userDataStore?.GetModInstallationHistory(ActiveGameFolderName) ?? [];
            var result = await Task.Run(() => service.Install(GameFolderPath, ActiveGameFolderName, ModFilePath, installationHistory));
            _userDataStore?.SaveModInstallation(ActiveGameFolderName, result.HistoryEntry);
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

    partial void OnPackedPsarcStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPackedPsarcStatus));
        OnPropertyChanged(nameof(IsPackedPsarcFailed));
    }

    partial void OnIsPackedPsarcSuccessfulChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPackedPsarcFailed));
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
