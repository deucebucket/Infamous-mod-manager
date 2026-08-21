using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using InfamousModManager.Services;
using Xunit;

namespace InfamousModManager.Tests;

public sealed class PackedPsarcProfileServiceTests
{
    [Fact]
    public void InstallsAuditedPairAndRestoresVerifiedRetailPair()
    {
        using var directory = new TemporaryDirectory();
        var gameRoot = Path.Combine(directory.Path, "game");
        var profile = Path.Combine(directory.Path, "profile");
        var backup = Path.Combine(directory.Path, "backup");
        Directory.CreateDirectory(profile);

        var retail1 = SyntheticPsarc.Build(["/A1.xpp"], ["retail-one"u8.ToArray()]);
        var retail2 = SyntheticPsarc.Build(["/A2.xpp"], ["retail-two"u8.ToArray()]);
        var mod1 = SyntheticPsarc.Build(["/A1.xpp"], ["mod-one"u8.ToArray()]);
        var mod2 = SyntheticPsarc.Build(["/A2.xpp"], ["mod-two"u8.ToArray()]);
        var live1 = WriteLive(gameRoot, "install1", "infamous1.psarc_s", retail1);
        var live2 = WriteLive(gameRoot, "install2", "infamous2.psarc_s", retail2);
        File.WriteAllBytes(Path.Combine(profile, "infamous1.psarc_s"), mod1);
        File.WriteAllBytes(Path.Combine(profile, "infamous2.psarc_s"), mod2);

        var specs = new[]
        {
            new PackedPsarcArchiveSpec("install1", "infamous1.psarc_s", Sha256(retail1)),
            new PackedPsarcArchiveSpec("install2", "infamous2.psarc_s", Sha256(retail2))
        };
        var service = new PackedPsarcProfileService(backup, () => false, specs);

        var installed = service.InstallProfile(gameRoot, "BCUS98119", profile);

        Assert.Equal("Installed", installed.Action);
        Assert.Equal(mod1, File.ReadAllBytes(live1));
        Assert.Equal(mod2, File.ReadAllBytes(live2));
        Assert.Equal(retail1, File.ReadAllBytes(Path.Combine(backup, "BCUS98119", "packed-retail", "install1", "infamous1.psarc_s")));

        var restored = service.RestoreRetail(gameRoot, "BCUS98119");

        Assert.Equal("Restored retail", restored.Action);
        Assert.Equal(retail1, File.ReadAllBytes(live1));
        Assert.Equal(retail2, File.ReadAllBytes(live2));
    }

    [Fact]
    public void RejectsProfileWithDifferentManifest()
    {
        using var directory = new TemporaryDirectory();
        var gameRoot = Path.Combine(directory.Path, "game");
        var profile = Path.Combine(directory.Path, "profile");
        var backup = Path.Combine(directory.Path, "backup");
        Directory.CreateDirectory(profile);
        var retail1 = SyntheticPsarc.Build(["/A1.xpp"], ["retail-one"u8.ToArray()]);
        var retail2 = SyntheticPsarc.Build(["/A2.xpp"], ["retail-two"u8.ToArray()]);
        WriteLive(gameRoot, "install1", "infamous1.psarc_s", retail1);
        WriteLive(gameRoot, "install2", "infamous2.psarc_s", retail2);
        File.WriteAllBytes(Path.Combine(profile, "infamous1.psarc_s"), SyntheticPsarc.Build(["/wrong.xpp"], ["mod"u8.ToArray()]));
        File.WriteAllBytes(Path.Combine(profile, "infamous2.psarc_s"), retail2);
        var service = new PackedPsarcProfileService(
            backup,
            () => false,
            [
                new("install1", "infamous1.psarc_s", Sha256(retail1)),
                new("install2", "infamous2.psarc_s", Sha256(retail2))
            ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            service.InstallProfile(gameRoot, "BCUS98119", profile));

        Assert.Contains("does not match the retail manifest", exception.Message);
        Assert.Equal(retail1, File.ReadAllBytes(Path.Combine(
            gameRoot, "BCUS98119", "USRDIR", "cache", "psarc", "install1", "infamous1.psarc_s")));
    }

    [Fact]
    public void UsesExplicitCleanRetailPairWhenLiveArchivesAreAlreadyModified()
    {
        using var directory = new TemporaryDirectory();
        var gameRoot = Path.Combine(directory.Path, "game");
        var profile = Path.Combine(directory.Path, "profile");
        var cleanRetail = Path.Combine(directory.Path, "retail");
        var backup = Path.Combine(directory.Path, "backup");
        Directory.CreateDirectory(profile);
        var retail1 = SyntheticPsarc.Build(["/A1.xpp"], ["retail-one"u8.ToArray()]);
        var retail2 = SyntheticPsarc.Build(["/A2.xpp"], ["retail-two"u8.ToArray()]);
        var previous1 = SyntheticPsarc.Build(["/A1.xpp"], ["previous-one"u8.ToArray()]);
        var previous2 = SyntheticPsarc.Build(["/A2.xpp"], ["previous-two"u8.ToArray()]);
        var mod1 = SyntheticPsarc.Build(["/A1.xpp"], ["mod-one"u8.ToArray()]);
        var mod2 = SyntheticPsarc.Build(["/A2.xpp"], ["mod-two"u8.ToArray()]);
        var live1 = WriteLive(gameRoot, "install1", "infamous1.psarc_s", previous1);
        var live2 = WriteLive(gameRoot, "install2", "infamous2.psarc_s", previous2);
        WritePair(cleanRetail, retail1, retail2);
        WritePair(profile, mod1, mod2);
        var service = new PackedPsarcProfileService(
            backup,
            () => false,
            [
                new("install1", "infamous1.psarc_s", Sha256(retail1)),
                new("install2", "infamous2.psarc_s", Sha256(retail2))
            ]);

        service.InstallProfile(gameRoot, "BCUS98119", profile, cleanRetail);

        Assert.Equal(mod1, File.ReadAllBytes(live1));
        Assert.Equal(mod2, File.ReadAllBytes(live2));
        service.RestoreRetail(gameRoot, "BCUS98119");
        Assert.Equal(retail1, File.ReadAllBytes(live1));
        Assert.Equal(retail2, File.ReadAllBytes(live2));
    }

    [Fact]
    public void RefusesArchiveSwapWhileRpcs3IsRunning()
    {
        using var directory = new TemporaryDirectory();
        var service = new PackedPsarcProfileService(directory.Path, () => true, []);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.InstallProfile(directory.Path, "BCUS98119", directory.Path));

        Assert.Contains("Close RPCS3", exception.Message);
    }

    private static string WriteLive(string gameRoot, string install, string name, byte[] data)
    {
        var directory = Path.Combine(gameRoot, "BCUS98119", "USRDIR", "cache", "psarc", install);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, data);
        return path;
    }

    private static void WritePair(string root, byte[] install1, byte[] install2)
    {
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "infamous1.psarc_s"), install1);
        File.WriteAllBytes(Path.Combine(root, "infamous2.psarc_s"), install2);
    }

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
}

internal static class SyntheticPsarc
{
    public static byte[] Build(IReadOnlyList<string> names, IReadOnlyList<byte[]> payloads)
    {
        const uint blockSize = 65536;
        var manifest = Encoding.UTF8.GetBytes(string.Join('\n', names));
        var files = new[] { manifest }.Concat(payloads).ToArray();
        var blocks = files.Select(Compress).ToArray();
        const int entrySize = 30;
        const int chunkWidth = 2;
        var tocLength = 32 + files.Length * entrySize + blocks.Length * chunkWidth;
        var offsets = new int[files.Length];
        var cursor = tocLength;
        for (var index = 0; index < blocks.Length; index++)
        {
            offsets[index] = cursor;
            cursor += blocks[index].Length;
        }

        using var output = new MemoryStream();
        output.Write("PSAR"u8);
        WriteUInt32(output, 0x00010003);
        output.Write("zlib"u8);
        WriteUInt32(output, checked((uint)tocLength));
        WriteUInt32(output, entrySize);
        WriteUInt32(output, checked((uint)files.Length));
        WriteUInt32(output, blockSize);
        WriteUInt32(output, 3);
        for (var index = 0; index < files.Length; index++)
        {
            output.Write(index == 0 ? new byte[16] : MD5.HashData(Encoding.UTF8.GetBytes(names[index - 1].ToUpperInvariant())));
            WriteUInt32(output, checked((uint)index));
            WriteUInt40(output, checked((ulong)files[index].Length));
            WriteUInt40(output, checked((ulong)offsets[index]));
        }
        var size = new byte[2];
        foreach (var block in blocks)
        {
            BinaryPrimitives.WriteUInt16BigEndian(size, checked((ushort)block.Length));
            output.Write(size);
        }
        foreach (var block in blocks)
        {
            output.Write(block);
        }
        return output.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data);
        }
        return output.ToArray();
    }

    private static void WriteUInt32(Stream output, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        output.Write(bytes);
    }

    private static void WriteUInt40(Stream output, ulong value)
    {
        Span<byte> bytes = stackalloc byte[5];
        bytes[0] = checked((byte)(value >> 32));
        bytes[1] = (byte)(value >> 24);
        bytes[2] = (byte)(value >> 16);
        bytes[3] = (byte)(value >> 8);
        bytes[4] = (byte)value;
        output.Write(bytes);
    }
}
