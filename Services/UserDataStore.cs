using System;
using System.IO;
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
                    IsGameFolderValid = gameFolder.IsGameFolderValid
                }
                : null;
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
            _data.GameFolders[gameFolderName] = new GameFolderData
            {
                GameFolderPath = gameFolderPath,
                IsGameFolderValid = isGameFolderValid
            };
            Save();
        }
    }

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
