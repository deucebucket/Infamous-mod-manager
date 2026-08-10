using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using InfamousModManager.Models;

namespace InfamousModManager.Services;

public sealed class GameModInstallationService
{
    public ModInstallationResult Install(
        string gameRoot,
        string gameFolderName,
        string selectedModPath,
        IReadOnlyCollection<ModInstallationHistoryEntry> installationHistory)
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
        var normalizedRelativePath = relativeOriginalPath.Replace('\\', '/');
        var previousFileSha256 = GetFileSha256(originalPath);
        var firstInstallation = installationHistory
            .Where(entry => string.Equals(entry.TargetRelativePath, normalizedRelativePath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.InstalledAtUtc)
            .FirstOrDefault();

        string originalFileSha256;
        string originalBackupRelativePath;
        if (firstInstallation is null)
        {
            originalFileSha256 = previousFileSha256;
            originalBackupRelativePath = BackupOriginalFile(gameFolderName, relativeOriginalPath, originalPath);
        }
        else
        {
            originalFileSha256 = firstInstallation.OriginalFileSha256;
            originalBackupRelativePath = firstInstallation.OriginalBackupRelativePath;
            VerifyOriginalBackup(originalBackupRelativePath, originalFileSha256);
        }

        ReplaceFile(originalPath, normalizedModPath);
        var installedFileSha256 = GetFileSha256(originalPath);

        return new ModInstallationResult(
            originalPath,
            new ModInstallationHistoryEntry
            {
                TargetRelativePath = normalizedRelativePath,
                OriginalFileSha256 = originalFileSha256,
                PreviousFileSha256 = previousFileSha256,
                InstalledFileSha256 = installedFileSha256,
                OriginalBackupRelativePath = originalBackupRelativePath,
                ModSourcePath = normalizedModPath,
                InstalledAtUtc = DateTimeOffset.UtcNow
            });
    }

    private static string BackupOriginalFile(string gameFolderName, string relativeOriginalPath, string originalPath)
    {
        var backupDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "backup",
            gameFolderName,
            "mods",
            Path.GetDirectoryName(relativeOriginalPath)!);
        Directory.CreateDirectory(backupDirectory);

        var backupPath = GetAvailableBackupPath(backupDirectory, Path.GetFileName(originalPath));
        File.Copy(originalPath, backupPath);
        var originalFileSha256 = GetFileSha256(originalPath);
        if (!string.Equals(GetFileSha256(backupPath), originalFileSha256, StringComparison.Ordinal))
        {
            File.Delete(backupPath);
            throw new IOException($"The backup verification failed for '{originalPath}'.");
        }

        return Path.GetRelativePath(AppContext.BaseDirectory, backupPath).Replace('\\', '/');
    }

    private static void VerifyOriginalBackup(string backupRelativePath, string expectedFileSha256)
    {
        var backupRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "backup")) + Path.DirectorySeparatorChar;
        var backupPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, backupRelativePath));
        if (!backupPath.StartsWith(backupRoot, StringComparison.Ordinal) || !File.Exists(backupPath))
        {
            throw new FileNotFoundException("The original backup required to install this mod was not found.", backupPath);
        }
        if (!string.Equals(GetFileSha256(backupPath), expectedFileSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The original backup hash does not match '{backupPath}'.");
        }
    }

    private static void ReplaceFile(string destinationPath, string sourcePath)
    {
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(sourcePath, temporaryPath);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string GetFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
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

public sealed record ModInstallationResult(string InstalledPath, ModInstallationHistoryEntry HistoryEntry);
