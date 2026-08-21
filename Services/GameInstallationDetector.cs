using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InfamousModManager.Models;

namespace InfamousModManager.Services;

public static class GameInstallationDetector
{
    public static GameInstallationDetection Detect(string gameRoot, IReadOnlyList<GameEdition> editions)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return GameInstallationDetection.Invalid("Select an existing RPCS3 dev_hdd0/game folder.");
        }

        var candidates = editions
            .Where(edition => Directory.Exists(Path.Combine(gameRoot, edition.TitleId)))
            .ToArray();
        if (candidates.Length == 0)
        {
            return GameInstallationDetection.Invalid(
                $"No supported installation was found. Expected: {string.Join(", ", editions.Select(edition => edition.TitleId))}.");
        }

        var complete = candidates
            .Where(edition => edition.RequiredRelativePaths.All(relativePath =>
                Path.Exists(Path.Combine(gameRoot, edition.TitleId, PathFromPortable(relativePath)))))
            .ToArray();
        if (complete.Length == 0)
        {
            var missing = candidates.SelectMany(edition => edition.RequiredRelativePaths
                .Where(relativePath => !Path.Exists(Path.Combine(gameRoot, edition.TitleId, PathFromPortable(relativePath))))
                .Select(relativePath => $"{edition.TitleId}/{relativePath}"));
            return GameInstallationDetection.Invalid($"The installation is incomplete. Missing: {string.Join(", ", missing)}.");
        }
        if (complete.Length > 1)
        {
            return GameInstallationDetection.Invalid(
                $"Multiple supported installations were found ({string.Join(", ", complete.Select(edition => edition.TitleId))}). Keep one under this root or select a root containing only the edition to manage.");
        }

        var detected = complete[0];
        var gameDirectory = Path.Combine(gameRoot, detected.TitleId);
        return new GameInstallationDetection(
            detected,
            gameDirectory,
            $"Detected {detected.DisplayName}: {gameDirectory}",
            true);
    }

    private static string PathFromPortable(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}

public sealed record GameInstallationDetection(
    GameEdition? Edition,
    string? GameDirectory,
    string Message,
    bool IsValid)
{
    public static GameInstallationDetection Invalid(string message) => new(null, null, message, false);
}
