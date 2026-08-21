using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using InfamousModManager.Models;

namespace InfamousModManager.Services;

public sealed class PackedPsarcProfileService
{
    private static readonly IReadOnlyList<PackedPsarcArchiveSpec> Bcus98119Archives =
    [
        new(
            "install1",
            "infamous1.psarc_s",
            "0DCF47869F6755A5CD596E67481D6F4574E81A2C2494C87B1E801A1B39C0ABBB"),
        new(
            "install2",
            "infamous2.psarc_s",
            "6AA4FC3C1CD6D7C0F6E7C9BDDF54BDF9039693BB52FC5DC0FA888C7BE60A4C4E")
    ];

    private readonly string _backupRoot;
    private readonly Func<bool> _isEmulatorRunning;
    private readonly IReadOnlyList<PackedPsarcArchiveSpec> _archives;
    private readonly PsarcArchiveInspector _inspector = new();

    public PackedPsarcProfileService(
        string? backupRoot = null,
        Func<bool>? isEmulatorRunning = null,
        IReadOnlyList<PackedPsarcArchiveSpec>? archives = null)
    {
        _backupRoot = backupRoot ?? Path.Combine(AppContext.BaseDirectory, "backup");
        _isEmulatorRunning = isEmulatorRunning ?? IsRpcs3Running;
        _archives = archives ?? Bcus98119Archives;
    }

    public PackedPsarcOperationResult InstallProfile(
        string gameRoot,
        string titleId,
        string profileDirectory,
        string? cleanRetailDirectory = null)
    {
        EnsureStopped();
        EnsureSupportedTitle(titleId);
        var destinations = ResolveLiveArchives(gameRoot, titleId);
        var profileSources = ResolveArchivePair(profileDirectory, "profile");
        var retailBackups = EnsureRetailBackups(titleId, destinations, cleanRetailDirectory);

        var retailInfo = InspectPair(retailBackups);
        var profileInfo = InspectPair(profileSources);
        CompareArchiveIdentity(retailInfo, profileInfo);
        ReplacePair(profileSources, destinations);

        var installedInfo = InspectPair(destinations);
        for (var index = 0; index < profileInfo.Count; index++)
        {
            if (!string.Equals(profileInfo[index].Sha256, installedInfo[index].Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"Installed archive verification failed for '{_archives[index].FileName}'.");
            }
        }

        return new PackedPsarcOperationResult(
            "Installed",
            profileInfo.Select((info, index) => new PackedPsarcInstalledArchive(
                _archives[index].FileName,
                info.Length,
                info.Sha256,
                info.Entries.Count)).ToArray());
    }

    public PackedPsarcOperationResult RestoreRetail(string gameRoot, string titleId)
    {
        EnsureStopped();
        EnsureSupportedTitle(titleId);
        var destinations = ResolveLiveArchives(gameRoot, titleId);
        var backups = ResolveBackupArchives(titleId);
        var backupInfo = InspectPair(backups);
        VerifyRetailHashes(backupInfo);
        ReplacePair(backups, destinations);

        return new PackedPsarcOperationResult(
            "Restored retail",
            backupInfo.Select((info, index) => new PackedPsarcInstalledArchive(
                _archives[index].FileName,
                info.Length,
                info.Sha256,
                info.Entries.Count)).ToArray());
    }

    private IReadOnlyList<string> EnsureRetailBackups(
        string titleId,
        IReadOnlyList<string> liveArchives,
        string? cleanRetailDirectory)
    {
        var backups = ResolveBackupArchives(titleId);
        var existingCount = backups.Count(File.Exists);
        if (existingCount == backups.Count)
        {
            VerifyRetailHashes(InspectPair(backups));
            return backups;
        }
        if (existingCount != 0)
        {
            throw new InvalidDataException("The protected retail backup pair is incomplete. Restore the missing backup before continuing.");
        }

        var sources = string.IsNullOrWhiteSpace(cleanRetailDirectory)
            ? liveArchives
            : ResolveArchivePair(cleanRetailDirectory, "clean retail source");
        var sourceInfo = InspectPair(sources);
        VerifyRetailHashes(sourceInfo);

        var createdBackups = new List<string>();
        try
        {
            for (var index = 0; index < sources.Count; index++)
            {
                CopyAtomically(sources[index], backups[index]);
                createdBackups.Add(backups[index]);
                var copiedHash = GetFileSha256(backups[index]);
                if (!string.Equals(copiedHash, _archives[index].RetailSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Retail backup verification failed for '{_archives[index].FileName}'.");
                }
            }
        }
        catch
        {
            foreach (var path in createdBackups)
            {
                File.Delete(path);
            }
            throw;
        }
        return backups;
    }

    private void CompareArchiveIdentity(
        IReadOnlyList<PsarcArchiveInfo> retail,
        IReadOnlyList<PsarcArchiveInfo> profile)
    {
        for (var index = 0; index < retail.Count; index++)
        {
            if (retail[index].BlockSize != profile[index].BlockSize ||
                !retail[index].Entries.SequenceEqual(profile[index].Entries, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Profile archive '{_archives[index].FileName}' does not match the retail manifest and layout.");
            }
        }

        var duplicateNames = profile
            .SelectMany(info => info.Entries)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateNames.Length > 0)
        {
            throw new InvalidDataException($"The PSARC pair contains ambiguous duplicate names: {string.Join(", ", duplicateNames)}.");
        }
    }

    private IReadOnlyList<PsarcArchiveInfo> InspectPair(IReadOnlyList<string> paths) =>
        paths.Select(_inspector.Inspect).ToArray();

    private void VerifyRetailHashes(IReadOnlyList<PsarcArchiveInfo> retailInfo)
    {
        for (var index = 0; index < retailInfo.Count; index++)
        {
            if (!string.Equals(retailInfo[index].Sha256, _archives[index].RetailSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"'{retailInfo[index].Path}' is not the verified retail {_archives[index].FileName}. " +
                    "Select the clean BCUS98119 retail PSARC pair before installing a profile.");
            }
        }
    }

    private IReadOnlyList<string> ResolveLiveArchives(string gameRoot, string titleId) =>
        _archives.Select(archive => Path.Combine(
            gameRoot,
            titleId,
            "USRDIR",
            "cache",
            "psarc",
            archive.InstallDirectory,
            archive.FileName)).ToArray();

    private IReadOnlyList<string> ResolveBackupArchives(string titleId) =>
        _archives.Select(archive => Path.Combine(
            _backupRoot,
            titleId,
            "packed-retail",
            archive.InstallDirectory,
            archive.FileName)).ToArray();

    private IReadOnlyList<string> ResolveArchivePair(string root, string description)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"The {description} directory was not found: '{root}'.");
        }

        return _archives.Select(archive =>
        {
            var flat = Path.Combine(root, archive.FileName);
            var nested = Path.Combine(root, archive.InstallDirectory, archive.FileName);
            var matches = new[] { flat, nested }.Where(File.Exists).Distinct(StringComparer.Ordinal).ToArray();
            return matches.Length switch
            {
                1 => matches[0],
                0 => throw new FileNotFoundException(
                    $"The {description} is missing '{archive.FileName}' (flat or {archive.InstallDirectory}/)."),
                _ => throw new InvalidDataException(
                    $"The {description} contains two copies of '{archive.FileName}'; remove the ambiguity.")
            };
        }).ToArray();
    }

    private static void ReplacePair(IReadOnlyList<string> sources, IReadOnlyList<string> destinations)
    {
        if (sources.Count != destinations.Count || destinations.Any(path => !File.Exists(path)))
        {
            throw new InvalidOperationException("The live packed archive pair is incomplete.");
        }

        var operationId = Guid.NewGuid().ToString("N");
        var staged = destinations.Select(path => $"{path}.{operationId}.staged").ToArray();
        var rollback = destinations.Select(path => $"{path}.{operationId}.previous").ToArray();
        try
        {
            for (var index = 0; index < sources.Count; index++)
            {
                CopyAndFlush(sources[index], staged[index]);
                if (!string.Equals(GetFileSha256(sources[index]), GetFileSha256(staged[index]), StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Staging verification failed for '{sources[index]}'.");
                }
            }
            for (var index = 0; index < destinations.Count; index++)
            {
                File.Move(destinations[index], rollback[index]);
            }
            for (var index = 0; index < destinations.Count; index++)
            {
                File.Move(staged[index], destinations[index]);
            }
            for (var index = 0; index < destinations.Count; index++)
            {
                if (!string.Equals(GetFileSha256(sources[index]), GetFileSha256(destinations[index]), StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Activation verification failed for '{destinations[index]}'.");
                }
            }
            foreach (var path in rollback)
            {
                File.Delete(path);
            }
        }
        catch
        {
            for (var index = 0; index < destinations.Count; index++)
            {
                if (File.Exists(rollback[index]))
                {
                    File.Delete(destinations[index]);
                    File.Move(rollback[index], destinations[index]);
                }
            }
            throw;
        }
        finally
        {
            foreach (var path in staged.Concat(rollback))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    private static void CopyAtomically(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            CopyAndFlush(source, temporary);
            File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static void CopyAndFlush(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var input = File.OpenRead(source);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
        output.Flush(flushToDisk: true);
    }

    private void EnsureStopped()
    {
        if (_isEmulatorRunning())
        {
            throw new InvalidOperationException("Close RPCS3 before installing or restoring packed archives.");
        }
    }

    private static void EnsureSupportedTitle(string titleId)
    {
        if (!string.Equals(titleId, GameEditions.Infamous1Bcus.TitleId, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Packed PSARC profiles are not supported for '{titleId}'.");
        }
    }

    private static bool IsRpcs3Running()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.ProcessName.Contains("rpcs3", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (OperatingSystem.IsLinux())
                {
                    var commandLinePath = $"/proc/{process.Id}/cmdline";
                    if (File.Exists(commandLinePath) &&
                        File.ReadAllText(commandLinePath).Contains("rpcs3", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                // The process exited while it was inspected.
            }
            finally
            {
                process.Dispose();
            }
        }
        return false;
    }

    private static string GetFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public sealed record PackedPsarcArchiveSpec(
    string InstallDirectory,
    string FileName,
    string RetailSha256);

public sealed record PackedPsarcInstalledArchive(
    string FileName,
    long Length,
    string Sha256,
    int EntryCount);

public sealed record PackedPsarcOperationResult(
    string Action,
    IReadOnlyList<PackedPsarcInstalledArchive> Archives);
