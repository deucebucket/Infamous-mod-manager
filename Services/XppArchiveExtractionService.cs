using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InfamousModManager.Models;

namespace InfamousModManager.Services;

public sealed class XppArchiveExtractionService
{
    private const uint PackMagic = 0x5041434B;
    private const uint MetadataResourceType = 0x02040000;
    private const uint VirtualResourceType = 0x0C100000;
    private const int CopyBufferSize = 1024 * 1024;

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true
    };

    public XppArchiveExtractionResult Extract(string archivePath, string outputRoot)
    {
        ValidateInput(archivePath, outputRoot);

        var sourcePath = Path.GetFullPath(archivePath);
        var destinationRoot = Path.GetFullPath(outputRoot);
        var outputName = $"{Path.GetFileName(sourcePath)}.unpacked";
        var outputDirectory = Path.Combine(destinationRoot, outputName);
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
        {
            throw new IOException($"The extraction output already exists: '{outputDirectory}'.");
        }

        Directory.CreateDirectory(destinationRoot);
        var stagingDirectory = Path.Combine(destinationRoot, $".{outputName}.{Guid.NewGuid():N}.tmp");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            using var archive = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var parsedArchive = ParseArchive(archive, sourcePath);
            var manifest = ExtractArchiveFiles(archive, parsedArchive, stagingDirectory);

            var manifestPath = Path.Combine(stagingDirectory, "manifest.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ManifestJsonOptions));
            Directory.Move(stagingDirectory, outputDirectory);

            return new XppArchiveExtractionResult(
                outputDirectory,
                manifest.Resources.Count(resource => resource.IsStoredRange),
                manifest.MetadataChunks.Count,
                manifest.IsEmptyStub);
        }
        catch
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }

            throw;
        }
    }

    private static ParsedXppArchive ParseArchive(Stream archive, string sourcePath)
    {
        if (archive.Length < 8)
        {
            throw new InvalidDataException("The selected file is too small to be an XPP/XPPS archive.");
        }

        Span<byte> prefix = stackalloc byte[8];
        ReadExactly(archive, prefix);
        var magic = BinaryPrimitives.ReadUInt32BigEndian(prefix);
        if (magic != PackMagic)
        {
            throw new InvalidDataException("The selected file does not contain the PACK signature.");
        }

        var versionAndHeaderSize = BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]);
        var formatVersion = checked((int)(versionAndHeaderSize >> 16));
        var headerSize = checked((int)(versionAndHeaderSize & 0xFFFF));
        var expectedHeaderSize = formatVersion switch
        {
            8 => 0x70,
            9 => 0x80,
            _ => throw new InvalidDataException($"Unsupported PACK format version: {formatVersion}.")
        };
        if (headerSize != expectedHeaderSize || headerSize > archive.Length || headerSize % 4 != 0)
        {
            throw new InvalidDataException($"Invalid PACK header size: 0x{headerSize:X}.");
        }

        archive.Position = 0;
        var headerBytes = ReadBytes(archive, headerSize);
        var headerWords = ReadBigEndianWords(headerBytes);
        var tableOffset = headerWords[6];
        var tableLength = headerWords[7];
        var payloadOffset = headerWords[10];
        var payloadLength = headerWords[11];
        var tableEnd = checked((ulong)tableOffset + tableLength);
        var isEmptyStub = payloadOffset == 0 && payloadLength == 0;

        if (tableOffset != headerSize || tableEnd > (ulong)archive.Length)
        {
            throw new InvalidDataException("The PACK table coordinates are outside the file.");
        }
        if (!isEmptyStub && (tableEnd != payloadOffset || (ulong)payloadOffset + payloadLength != (ulong)archive.Length))
        {
            throw new InvalidDataException("The PACK payload coordinates do not match the file length.");
        }
        if (isEmptyStub && tableEnd != (ulong)archive.Length)
        {
            throw new InvalidDataException("The empty PACK stub contains unexplained trailing data.");
        }

        archive.Position = tableOffset;
        var tableBytes = ReadBytes(archive, checked((int)tableLength));
        var tableWords = ReadBigEndianWords(tableBytes);
        if (tableWords.Count < 6)
        {
            throw new InvalidDataException("The PACK resource table is truncated.");
        }

        var descriptorCount = tableWords[1];
        var resourceCount = tableWords[3];
        var rangeRecordCount = tableWords[5];
        var rangeRecordWordCount = formatVersion == 8 ? 5u : 4u;
        var expectedTableWords = checked(6UL + descriptorCount * 7UL + resourceCount * 4UL + rangeRecordCount * rangeRecordWordCount);
        if (expectedTableWords != (ulong)tableWords.Count)
        {
            throw new InvalidDataException(
                $"The PACK table has an unexpected size: expected {expectedTableWords} words, found {tableWords.Count}.");
        }
        if (tableWords[0] != 0 || tableWords[2] != 0 || tableWords[4] != 0)
        {
            throw new InvalidDataException("The PACK table header contains unsupported non-zero reserved fields.");
        }

        var cursor = 6;
        var descriptors = new List<XppDescriptorManifest>(checked((int)descriptorCount));
        for (var index = 0; index < descriptorCount; index++)
        {
            var descriptor = new XppDescriptorManifest
            {
                Index = checked((int)index),
                Tag = tableWords[cursor],
                TagHex = ToHex(tableWords[cursor]),
                ByteLength = tableWords[cursor + 1],
                LogicalOffset = tableWords[cursor + 2],
                Reserved0 = tableWords[cursor + 3],
                Reserved1 = tableWords[cursor + 4],
                FirstResourceIndex = tableWords[cursor + 5],
                ResourceCount = tableWords[cursor + 6]
            };
            if (descriptor.Reserved0 != 0 || descriptor.Reserved1 != 0 ||
                (ulong)descriptor.FirstResourceIndex + descriptor.ResourceCount > resourceCount)
            {
                throw new InvalidDataException($"Invalid PACK descriptor at index {index}.");
            }

            descriptors.Add(descriptor);
            cursor += 7;
        }

        var resources = new List<XppResourceManifest>(checked((int)resourceCount));
        for (var index = 0; index < resourceCount; index++)
        {
            var byteLength = tableWords[cursor + 1];
            var logicalOffset = tableWords[cursor + 2];
            var isStoredRange = (ulong)logicalOffset + byteLength <= payloadLength;
            resources.Add(new XppResourceManifest
            {
                Index = checked((int)index),
                TypeAndFlags = tableWords[cursor],
                TypeAndFlagsHex = ToHex(tableWords[cursor]),
                ByteLength = byteLength,
                LogicalOffset = logicalOffset,
                Reserved = tableWords[cursor + 3],
                IsStoredRange = isStoredRange
            });
            if (tableWords[cursor + 3] != 0)
            {
                throw new InvalidDataException($"Resource {index} has an unsupported reserved value.");
            }

            cursor += 4;
        }

        ValidateDescriptors(descriptors, resources);

        var rangeRecords = new List<XppRangeRecordManifest>(checked((int)rangeRecordCount));
        uint expectedFirstIndex = 0;
        for (var index = 0; index < rangeRecordCount; index++)
        {
            uint? sentinel = formatVersion == 8 ? tableWords[cursor + 4] : null;
            var rangeRecord = new XppRangeRecordManifest
            {
                Index = checked((int)index),
                FirstIndex = tableWords[cursor],
                Count = tableWords[cursor + 1],
                Flags = tableWords[cursor + 2],
                FlagsHex = ToHex(tableWords[cursor + 2]),
                Reserved = tableWords[cursor + 3],
                Sentinel = sentinel
            };
            if (rangeRecord.FirstIndex != expectedFirstIndex || rangeRecord.Reserved != 0 ||
                (formatVersion == 8 && sentinel != uint.MaxValue))
            {
                throw new InvalidDataException($"Invalid PACK range record at index {index}.");
            }

            expectedFirstIndex = checked(rangeRecord.FirstIndex + rangeRecord.Count);
            rangeRecords.Add(rangeRecord);
            cursor += checked((int)rangeRecordWordCount);
        }

        return new ParsedXppArchive(
            sourcePath,
            formatVersion,
            headerSize,
            tableOffset,
            tableLength,
            payloadOffset,
            payloadLength,
            isEmptyStub,
            headerBytes,
            tableBytes,
            headerWords,
            new XppTableHeaderManifest
            {
                Zero0 = tableWords[0],
                DescriptorCount = descriptorCount,
                Zero1 = tableWords[2],
                ResourceCount = resourceCount,
                Zero2 = tableWords[4],
                RangeRecordCount = rangeRecordCount
            },
            descriptors,
            resources,
            rangeRecords);
    }

    private static XppArchiveManifest ExtractArchiveFiles(
        FileStream archive,
        ParsedXppArchive parsedArchive,
        string stagingDirectory)
    {
        File.WriteAllBytes(Path.Combine(stagingDirectory, "header.bin"), parsedArchive.HeaderBytes);
        File.WriteAllBytes(Path.Combine(stagingDirectory, "table.bin"), parsedArchive.TableBytes);

        var physicalPayloadOffset = parsedArchive.IsEmptyStub
            ? checked((long)parsedArchive.TableOffset + parsedArchive.TableLength)
            : parsedArchive.PayloadOffset;
        var payloadPath = Path.Combine(stagingDirectory, "payload.bin");
        CopyRange(archive, physicalPayloadOffset, parsedArchive.PayloadLength, payloadPath);

        var resourcesDirectory = Path.Combine(stagingDirectory, "resources");
        Directory.CreateDirectory(resourcesDirectory);
        var extractedResources = new List<XppResourceManifest>(parsedArchive.Resources.Count);
        var metadataChunks = new List<XppMetadataChunkManifest>();

        foreach (var resource in parsedArchive.Resources)
        {
            string? dataFile = null;
            string? resourceSha256 = null;
            if (resource.IsStoredRange)
            {
                var fileName = $"{resource.Index:D4}_{resource.TypeAndFlags:X8}_{resource.LogicalOffset:X8}_{resource.ByteLength:X8}.bin";
                dataFile = Path.Combine("resources", fileName).Replace('\\', '/');
                resourceSha256 = CopyRangeWithSha256(
                    archive,
                    checked((long)physicalPayloadOffset + resource.LogicalOffset),
                    resource.ByteLength,
                    Path.Combine(stagingDirectory, dataFile));

                if (resource.TypeAndFlags == MetadataResourceType)
                {
                    metadataChunks.AddRange(ExtractMetadataChunks(
                        archive,
                        parsedArchive,
                        resource,
                        physicalPayloadOffset,
                        stagingDirectory));
                }
            }

            extractedResources.Add(new XppResourceManifest
            {
                Index = resource.Index,
                TypeAndFlags = resource.TypeAndFlags,
                TypeAndFlagsHex = resource.TypeAndFlagsHex,
                ByteLength = resource.ByteLength,
                LogicalOffset = resource.LogicalOffset,
                Reserved = resource.Reserved,
                IsStoredRange = resource.IsStoredRange,
                DataFile = dataFile,
                Sha256 = resourceSha256
            });
        }

        archive.Position = 0;
        var sourceSha256 = Convert.ToHexString(SHA256.HashData(archive));
        return new XppArchiveManifest
        {
            SourceFileName = Path.GetFileName(parsedArchive.SourcePath),
            SourceSha256 = sourceSha256,
            SourceLength = archive.Length,
            FormatVersion = parsedArchive.FormatVersion,
            HeaderSize = parsedArchive.HeaderSize,
            TableOffset = parsedArchive.TableOffset,
            TableLength = parsedArchive.TableLength,
            PayloadOffset = parsedArchive.PayloadOffset,
            PayloadLength = parsedArchive.PayloadLength,
            IsEmptyStub = parsedArchive.IsEmptyStub,
            HeaderWords = parsedArchive.HeaderWords,
            TableHeader = parsedArchive.TableHeader,
            Descriptors = parsedArchive.Descriptors,
            Resources = extractedResources,
            RangeRecords = parsedArchive.RangeRecords,
            MetadataChunks = metadataChunks
        };
    }

    private static IEnumerable<XppMetadataChunkManifest> ExtractMetadataChunks(
        FileStream archive,
        ParsedXppArchive parsedArchive,
        XppResourceManifest resource,
        long physicalPayloadOffset,
        string stagingDirectory)
    {
        var resourceLength = checked((int)resource.ByteLength);
        archive.Position = checked(physicalPayloadOffset + resource.LogicalOffset);
        var metadata = ReadBytes(archive, resourceLength);
        var relativeDirectory = Path.Combine("metadata", $"resource_{resource.Index:D4}");
        Directory.CreateDirectory(Path.Combine(stagingDirectory, relativeDirectory));

        var chunks = new List<XppMetadataChunkManifest>();
        var cursor = 0;
        var chunkIndex = 0;
        while (cursor < metadata.Length)
        {
            if (metadata.Length - cursor < 8)
            {
                throw new InvalidDataException($"Metadata resource {resource.Index} has a truncated chunk header.");
            }

            var tagBytes = metadata.AsSpan(cursor, 4);
            var chunkLength = BinaryPrimitives.ReadUInt32BigEndian(metadata.AsSpan(cursor + 4, 4));
            var chunkEnd = checked((ulong)cursor + 8UL + chunkLength);
            if (chunkEnd > (ulong)metadata.Length)
            {
                throw new InvalidDataException($"Metadata resource {resource.Index} has an out-of-range chunk.");
            }

            var tag = Encoding.ASCII.GetString(tagBytes);
            var safeTag = new string(tag.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
            var fileName = $"{chunkIndex:D2}_{safeTag}.bin";
            var relativePath = Path.Combine(relativeDirectory, fileName).Replace('\\', '/');
            var chunkData = metadata.AsSpan(cursor + 8, checked((int)chunkLength)).ToArray();
            File.WriteAllBytes(Path.Combine(stagingDirectory, relativePath), chunkData);

            chunks.Add(new XppMetadataChunkManifest
            {
                ResourceIndex = resource.Index,
                ChunkIndex = chunkIndex,
                Tag = tag,
                ByteLength = chunkLength,
                ResourceRelativeOffset = checked((uint)cursor),
                DataFile = relativePath,
                Sha256 = Convert.ToHexString(SHA256.HashData(chunkData))
            });

            cursor = checked((int)chunkEnd);
            chunkIndex++;
        }

        return chunks;
    }

    private static void ValidateDescriptors(
        IReadOnlyList<XppDescriptorManifest> descriptors,
        IReadOnlyList<XppResourceManifest> resources)
    {
        foreach (var descriptor in descriptors)
        {
            if (descriptor.ResourceCount == 0)
            {
                if (descriptor.ByteLength != 0 || descriptor.LogicalOffset != 0)
                {
                    throw new InvalidDataException($"Empty descriptor {descriptor.Index} has a non-empty range.");
                }

                continue;
            }

            var firstIndex = checked((int)descriptor.FirstResourceIndex);
            var count = checked((int)descriptor.ResourceCount);
            var selectedResources = resources.Skip(firstIndex).Take(count).ToArray();
            if (selectedResources[0].LogicalOffset != descriptor.LogicalOffset)
            {
                throw new InvalidDataException($"Descriptor {descriptor.Index} starts at an unexpected offset.");
            }

            var physicalEnd = selectedResources
                .Where(resource => resource.IsStoredRange && resource.TypeAndFlags != VirtualResourceType)
                .Select(resource => (ulong)resource.LogicalOffset + resource.ByteLength)
                .DefaultIfEmpty(descriptor.LogicalOffset)
                .Max();
            if (physicalEnd - descriptor.LogicalOffset != descriptor.ByteLength)
            {
                throw new InvalidDataException($"Descriptor {descriptor.Index} has an unexpected length.");
            }
        }
    }

    private static void ValidateInput(string archivePath, string outputRoot)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("The selected XPP/XPPS archive was not found.", archivePath);
        }
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            throw new DirectoryNotFoundException("Select an extraction output folder.");
        }

        var extension = Path.GetExtension(archivePath);
        if (!string.Equals(extension, ".xpp", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".xpps", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The selected archive must have the .xpp or .xpps extension.");
        }
    }

    private static List<uint> ReadBigEndianWords(byte[] bytes)
    {
        if (bytes.Length % 4 != 0)
        {
            throw new InvalidDataException("A PACK structure is not aligned to four bytes.");
        }

        var words = new List<uint>(bytes.Length / 4);
        for (var offset = 0; offset < bytes.Length; offset += 4)
        {
            words.Add(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
        }

        return words;
    }

    private static byte[] ReadBytes(Stream stream, int count)
    {
        var bytes = new byte[count];
        ReadExactly(stream, bytes);
        return bytes;
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        var totalRead = 0;
        while (totalRead < destination.Length)
        {
            var read = stream.Read(destination[totalRead..]);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of PACK archive.");
            }

            totalRead += read;
        }
    }

    private static void CopyRange(Stream source, long offset, uint length, string destinationPath) =>
        CopyRangeCore(source, offset, length, destinationPath, hash: false);

    private static string CopyRangeWithSha256(Stream source, long offset, uint length, string destinationPath) =>
        CopyRangeCore(source, offset, length, destinationPath, hash: true)!;

    private static string? CopyRangeCore(Stream source, long offset, uint length, string destinationPath, bool hash)
    {
        source.Position = offset;
        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var hasher = hash ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
        var buffer = new byte[CopyBufferSize];
        long remaining = length;
        while (remaining > 0)
        {
            var requested = (int)Math.Min(buffer.Length, remaining);
            var read = source.Read(buffer, 0, requested);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of PACK payload.");
            }

            destination.Write(buffer, 0, read);
            hasher?.AppendData(buffer, 0, read);
            remaining -= read;
        }

        return hasher is null ? null : Convert.ToHexString(hasher.GetHashAndReset());
    }

    private static string ToHex(uint value) => $"0x{value:X8}";

    private sealed record ParsedXppArchive(
        string SourcePath,
        int FormatVersion,
        int HeaderSize,
        uint TableOffset,
        uint TableLength,
        uint PayloadOffset,
        uint PayloadLength,
        bool IsEmptyStub,
        byte[] HeaderBytes,
        byte[] TableBytes,
        List<uint> HeaderWords,
        XppTableHeaderManifest TableHeader,
        List<XppDescriptorManifest> Descriptors,
        List<XppResourceManifest> Resources,
        List<XppRangeRecordManifest> RangeRecords);
}

public sealed record XppArchiveExtractionResult(
    string OutputDirectory,
    int ExtractedResourceCount,
    int MetadataChunkCount,
    bool IsEmptyStub);
