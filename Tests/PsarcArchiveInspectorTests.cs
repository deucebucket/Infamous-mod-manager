using System;
using System.IO;
using InfamousModManager.Services;
using Xunit;

namespace InfamousModManager.Tests;

public sealed class PsarcArchiveInspectorTests
{
    [Fact]
    public void AuditsConfiguredRetailAndProfilePairs()
    {
        var retailRoot = Environment.GetEnvironmentVariable("IMM_RETAIL_PSARC_ROOT");
        var profileRoot = Environment.GetEnvironmentVariable("IMM_PROFILE_PSARC_ROOT");
        if (string.IsNullOrWhiteSpace(retailRoot) || string.IsNullOrWhiteSpace(profileRoot))
        {
            return;
        }

        var inspector = new PsarcArchiveInspector();
        foreach (var pair in new[] { ("install1", "infamous1.psarc_s"), ("install2", "infamous2.psarc_s") })
        {
            var retail = inspector.Inspect(Path.Combine(retailRoot, pair.Item1, pair.Item2));
            var profilePath = File.Exists(Path.Combine(profileRoot, pair.Item2))
                ? Path.Combine(profileRoot, pair.Item2)
                : Path.Combine(profileRoot, pair.Item1, pair.Item2);
            var profile = inspector.Inspect(profilePath);

            Assert.Equal(retail.BlockSize, profile.BlockSize);
            Assert.Equal(retail.Entries, profile.Entries);
        }
    }
}
