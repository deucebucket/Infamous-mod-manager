using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace InfamousModManager.Models;

public sealed class PsarcArchiveExtractor
{
    public int Extract(string archivePath, string outputDirectory)
    {
        using var archive = File.OpenRead(archivePath);
        var header = ReadExactly(archive, 32);

        if (Encoding.ASCII.GetString(header, 0, 4) != "PSAR")
        {
            throw new InvalidDataException("The selected file is not a PSARC archive.");
        }

        var compression = Encoding.ASCII.GetString(header, 8, 4);
        var dataOffset = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(12, 4));
        var entrySize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16, 4));
        var entryCount = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20, 4));
        var blockSize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(24, 4));

        if (dataOffset < 32 || entrySize < 30 || entryCount < 2 || blockSize == 0)
        {
            throw new InvalidDataException("The PSARC header is invalid.");
        }
        if (compression is not "zlib" and not "\0\0\0\0")
        {
            throw new NotSupportedException($"PSARC compression '{compression}' is not supported.");
        }

        var descriptorsLength = checked((int)(entrySize * entryCount));
        var chunkTableLength = checked((int)dataOffset - 32 - descriptorsLength);
        if (chunkTableLength < 0)
        {
            throw new InvalidDataException("The PSARC table of contents is invalid.");
        }

        var descriptors = ReadExactly(archive, descriptorsLength);
        var entries = ParseEntries(descriptors, checked((int)entrySize), checked((int)entryCount));
        var chunkSizeWidth = GetChunkSizeWidth(blockSize);
        if (chunkTableLength % chunkSizeWidth != 0)
        {
            throw new InvalidDataException("The PSARC compression directory is invalid.");
        }

        var chunkSizes = ParseChunkSizes(ReadExactly(archive, chunkTableLength), chunkSizeWidth);
        var manifest = ReadFile(archive, entries[0], chunkSizes, blockSize, compression == "zlib");
        var fileNames = Encoding.UTF8.GetString(manifest)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (fileNames.Length != entries.Count - 1)
        {
            throw new InvalidDataException("The PSARC manifest does not match the archive entries.");
        }

        Directory.CreateDirectory(outputDirectory);
        var outputRoot = Path.GetFullPath(outputDirectory) + Path.DirectorySeparatorChar;
        var extractedFiles = 0;

        for (var index = 1; index < entries.Count; index++)
        {
            var relativePath = GetSafeXppsPath(fileNames[index - 1]);
            if (relativePath is null)
            {
                continue;
            }

            var destinationPath = Path.GetFullPath(Path.Combine(outputRoot, relativePath));
            if (!destinationPath.StartsWith(outputRoot, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The PSARC manifest contains an unsafe file path.");
            }

            var fileData = ReadFile(archive, entries[index], chunkSizes, blockSize, compression == "zlib");
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.WriteAllBytes(destinationPath, fileData);
            extractedFiles++;
        }

        if (extractedFiles == 0)
        {
            throw new InvalidDataException("The PSARC archive does not contain XPPS files.");
        }

        return extractedFiles;
    }

    private static byte[] ReadFile(Stream archive, PsarcEntry entry, IReadOnlyList<uint> chunkSizes, uint blockSize, bool isZlibCompressed)
    {
        var chunkCount = checked((int)((entry.Length + blockSize - 1) / blockSize));
        if ((ulong)entry.ChunkIndex + (uint)chunkCount > (ulong)chunkSizes.Count)
        {
            throw new InvalidDataException("The PSARC chunk index is invalid.");
        }

        archive.Position = checked((long)entry.Offset);
        using var output = new MemoryStream(checked((int)entry.Length));

        for (var index = 0; index < chunkCount; index++)
        {
            var remaining = entry.Length - (ulong)output.Length;
            var uncompressedLength = checked((int)Math.Min((ulong)blockSize, remaining));
            var compressedLength = chunkSizes[checked((int)entry.ChunkIndex + index)];
            var storedLength = compressedLength == 0 ? uncompressedLength : checked((int)compressedLength);
            var block = ReadExactly(archive, storedLength);

            if (isZlibCompressed && compressedLength != 0 && IsZlibStream(block))
            {
                using var compressedStream = new MemoryStream(block);
                using var zlib = new ZLibStream(compressedStream, CompressionMode.Decompress);
                zlib.CopyTo(output);
            }
            else
            {
                if (block.Length != uncompressedLength)
                {
                    throw new InvalidDataException("The PSARC block is neither a valid zlib stream nor an uncompressed block.");
                }

                output.Write(block);
            }
        }

        if ((ulong)output.Length != entry.Length)
        {
            throw new InvalidDataException("The extracted PSARC file has an unexpected length.");
        }

        return output.ToArray();
    }

    private static bool IsZlibStream(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2 || (data[0] & 0x0F) != 8)
        {
            return false;
        }

        return ((data[0] << 8) | data[1]) % 31 == 0;
    }

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

    private static string? GetSafeXppsPath(string manifestPath)
    {
        var relativePath = manifestPath.Replace('\\', '/').TrimStart('/');
        if (!relativePath.EndsWith(".xpps", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (relativePath.StartsWith("../", StringComparison.Ordinal) ||
            relativePath.Contains("/../", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The PSARC manifest contains an unsafe file path.");
        }

        return relativePath.Replace('/', Path.DirectorySeparatorChar);
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
