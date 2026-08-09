using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InfamousModManager.Models;

namespace InfamousModManager.Services;

public sealed class GamePsarcExtractionService
{
    public async Task<int> ExtractInstallArchivesAsync(string gameRoot, string gameFolderName)
    {
        var gameDirectory = Path.Combine(gameRoot, gameFolderName);
        var outputDirectory = Path.Combine(gameDirectory, "USRDIR");
        var archives = new List<(string InstallName, string ArchivePath)>();

        foreach (var installName in new[] { "install1", "install2" })
        {
            var installDirectory = Path.Combine(outputDirectory, "cache", "psarc", installName);
            var matches = Directory.Exists(installDirectory)
                ? Directory.EnumerateFiles(installDirectory, "*.psarc_s", SearchOption.TopDirectoryOnly).ToArray()
                : [];

            if (matches.Length != 1)
            {
                throw new InvalidOperationException($"Expected exactly one .psarc_s file in '{installDirectory}'.");
            }

            archives.Add((installName, matches[0]));
        }

        var extractor = new PsarcArchiveExtractor();
        var extractedFiles = await Task.Run(() => archives.Sum(archive => extractor.Extract(archive.ArchivePath, outputDirectory)));

        foreach (var archive in archives)
        {
            var backupDirectory = Path.Combine(AppContext.BaseDirectory, "backup", gameFolderName, archive.InstallName);
            Directory.CreateDirectory(backupDirectory);
            var backupPath = GetAvailableBackupPath(backupDirectory, Path.GetFileName(archive.ArchivePath));
            File.Move(archive.ArchivePath, backupPath);
        }

        return extractedFiles;
    }

    private static string GetAvailableBackupPath(string backupDirectory, string fileName)
    {
        var candidate = Path.Combine(backupDirectory, fileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        return Path.Combine(backupDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}-{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(fileName)}");
    }
}
