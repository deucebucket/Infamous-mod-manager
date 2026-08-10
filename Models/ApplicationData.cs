using System;
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

    public List<ModInstallationHistoryEntry> ModInstallationHistory { get; set; } = [];
}

public sealed class ModInstallationHistoryEntry
{
    public string TargetRelativePath { get; set; } = string.Empty;

    public string OriginalFileSha256 { get; set; } = string.Empty;

    public string PreviousFileSha256 { get; set; } = string.Empty;

    public string InstalledFileSha256 { get; set; } = string.Empty;

    // This path is relative to the application directory so the data remains portable.
    public string OriginalBackupRelativePath { get; set; } = string.Empty;

    public string ModSourcePath { get; set; } = string.Empty;

    public DateTimeOffset InstalledAtUtc { get; set; }
}
