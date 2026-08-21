using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using InfamousModManager.Models;

namespace InfamousModManager.Services;

public sealed class PsarcArchiveInspector
{
    public PsarcArchiveInfo Inspect(string archivePath)
    {
        using var archive = File.OpenRead(archivePath);
        var header = ReadExactly(archive, 32);
        if (Encoding.ASCII.GetString(header, 0, 4) != "PSAR")
        {
            throw new InvalidDataException($"'{archivePath}' is not a PSARC archive.");
        }

        var compression = Encoding.ASCII.GetString(header, 8, 4);
        var dataOffset = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(12, 4));
        var entrySize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16, 4));
        var entryCount = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20, 4));
        var blockSize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(24, 4));
        if (dataOffset < 32 || entrySize < 30 || entryCount < 2 || blockSize == 0 || dataOffset > archive.Length)
        {
            throw new InvalidDataException($"'{archivePath}' has an invalid PSARC header.");
        }
        if (compression is not "zlib" and not "\0\0\0\0")
        {
            throw new NotSupportedException($"PSARC compression '{compression}' is not supported.");
        }

        var descriptorsLength = checked((int)(entrySize * entryCount));
        var chunkTableLength = checked((int)dataOffset - 32 - descriptorsLength);
        var chunkSizeWidth = GetChunkSizeWidth(blockSize);
        if (chunkTableLength < 0 || chunkTableLength % chunkSizeWidth != 0)
        {
            throw new InvalidDataException($"'{archivePath}' has an invalid PSARC table of contents.");
        }

        var entries = ParseEntries(ReadExactly(archive, descriptorsLength), checked((int)entrySize), checked((int)entryCount));
        var chunkSizes = ParseChunkSizes(ReadExactly(archive, chunkTableLength), chunkSizeWidth);
        foreach (var entry in entries)
        {
            ValidateEntryBounds(entry, chunkSizes, blockSize, archive.Length);
        }

        var manifest = ReadFile(archive, entries[0], chunkSizes, blockSize, compression == "zlib");
        var names = Encoding.UTF8.GetString(manifest)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (names.Length != entries.Count - 1)
        {
            throw new InvalidDataException($"'{archivePath}' has a manifest that does not match its entry table.");
        }
        if (names.Any(name => string.IsNullOrWhiteSpace(name) || name.Contains('\0')))
        {
            throw new InvalidDataException($"'{archivePath}' contains an invalid manifest path.");
        }

        archive.Position = 0;
        var sha256 = Convert.ToHexString(SHA256.HashData(archive));
        return new PsarcArchiveInfo(archivePath, archive.Length, sha256, blockSize, names);
    }

    private static void ValidateEntryBounds(PsarcEntry entry, IReadOnlyList<uint> chunkSizes, uint blockSize, long archiveLength)
    {
        var chunkCount = checked((int)((entry.Length + blockSize - 1) / blockSize));
        if ((ulong)entry.ChunkIndex + (uint)chunkCount > (ulong)chunkSizes.Count || entry.Offset > (ulong)archiveLength)
        {
            throw new InvalidDataException("A PSARC entry points outside the archive.");
        }

        ulong storedLength = 0;
        ulong remaining = entry.Length;
        for (var index = 0; index < chunkCount; index++)
        {
            var logicalLength = Math.Min((ulong)blockSize, remaining);
            var compressedLength = chunkSizes[checked((int)entry.ChunkIndex + index)];
            storedLength = checked(storedLength + (compressedLength == 0 ? logicalLength : compressedLength));
            remaining -= logicalLength;
        }
        if (entry.Offset + storedLength > (ulong)archiveLength)
        {
            throw new InvalidDataException("A PSARC entry payload extends outside the archive.");
        }
    }

    private static byte[] ReadFile(Stream archive, PsarcEntry entry, IReadOnlyList<uint> chunkSizes, uint blockSize, bool isZlibCompressed)
    {
        var chunkCount = checked((int)((entry.Length + blockSize - 1) / blockSize));
        archive.Position = checked((long)entry.Offset);
        using var output = new MemoryStream(checked((int)entry.Length));
        for (var index = 0; index < chunkCount; index++)
        {
            var remaining = entry.Length - (ulong)output.Length;
            var logicalLength = checked((int)Math.Min((ulong)blockSize, remaining));
            var compressedLength = chunkSizes[checked((int)entry.ChunkIndex + index)];
            var storedLength = compressedLength == 0 ? logicalLength : checked((int)compressedLength);
            var block = ReadExactly(archive, storedLength);
            if (isZlibCompressed && compressedLength != 0 && IsZlibStream(block))
            {
                using var compressedStream = new MemoryStream(block);
                using var zlib = new ZLibStream(compressedStream, CompressionMode.Decompress);
                zlib.CopyTo(output);
            }
            else
            {
                if (block.Length != logicalLength)
                {
                    throw new InvalidDataException("A PSARC manifest block is neither valid zlib nor the expected uncompressed length.");
                }
                output.Write(block);
            }
        }
        if ((ulong)output.Length != entry.Length)
        {
            throw new InvalidDataException("The PSARC manifest has an unexpected length.");
        }
        return output.ToArray();
    }

    private static bool IsZlibStream(ReadOnlySpan<byte> data) =>
        data.Length >= 2 && (data[0] & 0x0F) == 8 && ((data[0] << 8) | data[1]) % 31 == 0;

    private static List<PsarcEntry> ParseEntries(byte[] descriptors, int entrySize, int entryCount)
    {
        var entries = new List<PsarcEntry>(entryCount);
        for (var index = 0; index < entryCount; index++)
        {
            var entry = descriptors.AsSpan(index * entrySize, entrySize);
            entries.Add(new PsarcEntry(
                BinaryPrimitives.ReadUInt32BigEndian(entry.Slice(16, 4)),
                ReadUInt40BigEndian(entry.Slice(20, 5)),
                ReadUInt40BigEndian(entry.Slice(25, 5))));
        }
        return entries;
    }

    private static List<uint> ParseChunkSizes(byte[] data, int width)
    {
        var sizes = new List<uint>(data.Length / width);
        for (var offset = 0; offset < data.Length; offset += width)
        {
            uint size = 0;
            for (var index = 0; index < width; index++)
            {
                size = (size << 8) | data[offset + index];
            }
            sizes.Add(size);
        }
        return sizes;
    }

    private static int GetChunkSizeWidth(uint blockSize)
    {
        var width = 1;
        while ((1UL << (width * 8)) < blockSize)
        {
            width++;
        }
        return width;
    }

    private static byte[] ReadExactly(Stream stream, int length)
    {
        var buffer = new byte[length];
        stream.ReadExactly(buffer);
        return buffer;
    }

    private static ulong ReadUInt40BigEndian(ReadOnlySpan<byte> data) =>
        ((ulong)data[0] << 32) | ((ulong)data[1] << 24) | ((ulong)data[2] << 16) | ((ulong)data[3] << 8) | data[4];

    private readonly record struct PsarcEntry(uint ChunkIndex, ulong Length, ulong Offset);
}
