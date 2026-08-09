using System.Collections.Generic;

namespace InfamousModManager.Models;

public sealed class ApplicationData
{
    public string SelectedLanguageCode { get; set; } = "en-US";

    public Dictionary<string, GameFolderData> GameFolders { get; set; } = new();
}

public sealed class GameFolderData
{
    public string GameFolderPath { get; set; } = string.Empty;

    public bool IsGameFolderValid { get; set; }
}
