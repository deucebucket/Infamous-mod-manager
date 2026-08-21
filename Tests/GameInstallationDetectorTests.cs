using System;
using System.IO;
using InfamousModManager.Models;
using InfamousModManager.Services;
using Xunit;

namespace InfamousModManager.Tests;

public sealed class GameInstallationDetectorTests
{
    [Fact]
    public void DetectsExistingPsnEditionWithoutChangingItsWorkflow()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "NPUA80480", "USRDIR"));

        var result = GameInstallationDetector.Detect(
            directory.Path,
            [GameEditions.Infamous1Psn, GameEditions.Infamous1Bcus]);

        Assert.True(result.IsValid);
        Assert.Equal("NPUA80480", result.Edition?.TitleId);
        Assert.Equal(GameModMode.LooseXpps, result.Edition?.ModMode);
    }

    [Fact]
    public void DetectsCompleteBcusPackedEdition()
    {
        using var directory = new TemporaryDirectory();
        CreateBcusLayout(directory.Path);

        var result = GameInstallationDetector.Detect(
            directory.Path,
            [GameEditions.Infamous1Psn, GameEditions.Infamous1Bcus]);

        Assert.True(result.IsValid);
        Assert.Equal("BCUS98119", result.Edition?.TitleId);
        Assert.Equal(GameModMode.PackedPsarc, result.Edition?.ModMode);
    }

    [Fact]
    public void RejectsAmbiguousEditionsInsteadOfGuessing()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "NPUA80480", "USRDIR"));
        CreateBcusLayout(directory.Path);

        var result = GameInstallationDetector.Detect(
            directory.Path,
            [GameEditions.Infamous1Psn, GameEditions.Infamous1Bcus]);

        Assert.False(result.IsValid);
        Assert.Contains("Multiple supported installations", result.Message);
    }

    [Fact]
    public void RejectsIncompletePackedEdition()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(
            directory.Path,
            "BCUS98119",
            "USRDIR",
            "cache",
            "psarc",
            "install1"));

        var result = GameInstallationDetector.Detect(directory.Path, [GameEditions.Infamous1Bcus]);

        Assert.False(result.IsValid);
        Assert.Contains("incomplete", result.Message);
        Assert.Contains("infamous2.psarc_s", result.Message);
    }

    private static void CreateBcusLayout(string root)
    {
        foreach (var pair in new[] { ("install1", "infamous1.psarc_s"), ("install2", "infamous2.psarc_s") })
        {
            var directory = Path.Combine(root, "BCUS98119", "USRDIR", "cache", "psarc", pair.Item1);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, pair.Item2), [0]);
        }
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"infamous-mod-manager-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
