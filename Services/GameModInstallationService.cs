using System;
using System.IO;
using System.Linq;

namespace InfamousModManager.Services;

public sealed class GameModInstallationService
{
    public string Install(string gameRoot, string gameFolderName, string selectedModPath)
    {
        if (!File.Exists(selectedModPath))
        {
            throw new FileNotFoundException("The selected mod file was not found.", selectedModPath);
        }

        var normalizedModPath = NormalizeModFileName(selectedModPath);
        var unpackedFilesDirectory = Path.Combine(gameRoot, gameFolderName, "USRDIR");
        if (!Directory.Exists(unpackedFilesDirectory))
        {
            throw new DirectoryNotFoundException($"The unpacked game directory was not found: '{unpackedFilesDirectory}'.");
        }

        var normalizedName = Path.GetFileName(normalizedModPath);
        var matchingFiles = Directory.EnumerateFiles(unpackedFilesDirectory, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetFileName(path), normalizedName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matchingFiles.Length == 0)
        {
            throw new FileNotFoundException($"No original file matching '{normalizedName}' was found.");
        }
        if (matchingFiles.Length > 1)
        {
            throw new InvalidOperationException($"More than one original file matches '{normalizedName}'.");
        }

        var originalPath = matchingFiles[0];
        var relativeOriginalPath = Path.GetRelativePath(unpackedFilesDirectory, originalPath);
        var backupDirectory = Path.Combine(AppContext.BaseDirectory, "backup", gameFolderName, "mods", Path.GetDirectoryName(relativeOriginalPath)!);
        Directory.CreateDirectory(backupDirectory);

        var backupPath = GetAvailableBackupPath(backupDirectory, Path.GetFileName(originalPath));
        File.Move(originalPath, backupPath);

        try
        {
            File.Copy(normalizedModPath, originalPath);
        }
        catch
        {
            File.Move(backupPath, originalPath);
            throw;
        }

        return originalPath;
    }

    private static string NormalizeModFileName(string selectedModPath)
    {
        if (!string.Equals(Path.GetExtension(selectedModPath), ".xpps", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The selected mod must have the .xpps extension.");
        }

        var normalizedName = $"{Path.GetFileNameWithoutExtension(selectedModPath).Replace(' ', '_').ToLowerInvariant()}.xpps";
        var normalizedPath = Path.Combine(Path.GetDirectoryName(selectedModPath)!, normalizedName);
        if (string.Equals(selectedModPath, normalizedPath, StringComparison.Ordinal))
        {
            return normalizedPath;
        }
        if (File.Exists(normalizedPath) && !string.Equals(selectedModPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"Cannot rename the mod: '{normalizedName}' already exists.");
        }

        File.Move(selectedModPath, normalizedPath, overwrite: true);
        return normalizedPath;
    }

    private static string GetAvailableBackupPath(string backupDirectory, string fileName)
    {
        var candidate = Path.Combine(backupDirectory, fileName);
        return File.Exists(candidate)
            ? Path.Combine(backupDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}-{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(fileName)}")
            : candidate;
    }
}
