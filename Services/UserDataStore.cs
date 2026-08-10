using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using InfamousModManager.Models;

namespace InfamousModManager.Services;

public sealed class UserDataStore
{
    private readonly object _syncRoot = new();
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private ApplicationData _data;

    public UserDataStore()
    {
        var dataDirectory = AppContext.BaseDirectory;
        FilePath = Path.Combine(dataDirectory, "user-data.json");
        Directory.CreateDirectory(dataDirectory);
        _data = LoadOrCreate();
    }

    public string FilePath { get; }

    public string SelectedLanguageCode
    {
        get
        {
            lock (_syncRoot)
            {
                return _data.SelectedLanguageCode;
            }
        }
    }

    public GameFolderData? GetGameFolder(string gameFolderName)
    {
        lock (_syncRoot)
        {
            return _data.GameFolders.TryGetValue(gameFolderName, out var gameFolder)
                ? new GameFolderData
                {
                    GameFolderPath = gameFolder.GameFolderPath,
                    IsGameFolderValid = gameFolder.IsGameFolderValid,
                    ModInstallationHistory = (gameFolder.ModInstallationHistory ?? [])
                        .Select(CloneHistoryEntry)
                        .ToList()
                }
                : null;
        }
    }

    public IReadOnlyList<ModInstallationHistoryEntry> GetModInstallationHistory(string gameFolderName)
    {
        lock (_syncRoot)
        {
            return _data.GameFolders.TryGetValue(gameFolderName, out var gameFolder)
                ? (gameFolder.ModInstallationHistory ?? []).Select(CloneHistoryEntry).ToArray()
                : [];
        }
    }

    public void SaveSelectedLanguage(string languageCode)
    {
        lock (_syncRoot)
        {
            _data.SelectedLanguageCode = languageCode;
            Save();
        }
    }

    public void SaveGameFolder(string gameFolderName, string gameFolderPath, bool isGameFolderValid)
    {
        lock (_syncRoot)
        {
            var gameFolder = GetOrCreateGameFolder(gameFolderName);
            gameFolder.GameFolderPath = gameFolderPath;
            gameFolder.IsGameFolderValid = isGameFolderValid;
            Save();
        }
    }

    public void SaveModInstallation(string gameFolderName, ModInstallationHistoryEntry historyEntry)
    {
        lock (_syncRoot)
        {
            var gameFolder = GetOrCreateGameFolder(gameFolderName);
            gameFolder.ModInstallationHistory.Add(CloneHistoryEntry(historyEntry));
            Save();
        }
    }

    private GameFolderData GetOrCreateGameFolder(string gameFolderName)
    {
        if (_data.GameFolders.TryGetValue(gameFolderName, out var gameFolder))
        {
            gameFolder.ModInstallationHistory ??= [];
            return gameFolder;
        }

        gameFolder = new GameFolderData();
        _data.GameFolders.Add(gameFolderName, gameFolder);
        return gameFolder;
    }

    private static ModInstallationHistoryEntry CloneHistoryEntry(ModInstallationHistoryEntry entry) => new()
    {
        TargetRelativePath = entry.TargetRelativePath,
        OriginalFileSha256 = entry.OriginalFileSha256,
        PreviousFileSha256 = entry.PreviousFileSha256,
        InstalledFileSha256 = entry.InstalledFileSha256,
        OriginalBackupRelativePath = entry.OriginalBackupRelativePath,
        ModSourcePath = entry.ModSourcePath,
        InstalledAtUtc = entry.InstalledAtUtc
    };

    private ApplicationData LoadOrCreate()
    {
        if (!File.Exists(FilePath))
        {
            var newData = new ApplicationData();
            WriteData(newData);
            return newData;
        }

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<ApplicationData>(json) ?? new ApplicationData();
    }

    private void Save() => WriteData(_data);

    private void WriteData(ApplicationData data)
    {
        var temporaryFilePath = $"{FilePath}.tmp";
        File.WriteAllText(temporaryFilePath, JsonSerializer.Serialize(data, _serializerOptions));
        File.Move(temporaryFilePath, FilePath, overwrite: true);
    }
}
