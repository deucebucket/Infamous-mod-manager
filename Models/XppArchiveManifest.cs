using System.Collections.Generic;

namespace InfamousModManager.Models;

public sealed class XppArchiveManifest
{
    public const int CurrentManifestVersion = 2;

    public int ManifestVersion { get; init; } = CurrentManifestVersion;

    public string SourceFileName { get; init; } = string.Empty;

    public string SourceSha256 { get; init; } = string.Empty;

    public long SourceLength { get; init; }

    public int FormatVersion { get; init; }

    public int HeaderSize { get; init; }

    public uint TableOffset { get; init; }

    public uint TableLength { get; init; }

    public uint PayloadOffset { get; init; }

    public uint PayloadLength { get; init; }

    public bool IsEmptyStub { get; init; }

    public string HeaderFile { get; init; } = "header.bin";

    public string TableFile { get; init; } = "table.bin";

    public string PayloadFile { get; init; } = "payload.bin";

    public List<uint> HeaderWords { get; init; } = [];

    public XppTableHeaderManifest TableHeader { get; init; } = new();

    public List<XppDescriptorManifest> Descriptors { get; init; } = [];

    public List<XppResourceManifest> Resources { get; init; } = [];

    public List<XppRangeRecordManifest> RangeRecords { get; init; } = [];

    public List<XppMetadataChunkManifest> MetadataChunks { get; init; } = [];

    public List<XppTextureManifest> Textures { get; init; } = [];
}

public sealed class XppTableHeaderManifest
{
    public uint Zero0 { get; init; }

    public uint DescriptorCount { get; init; }

    public uint Zero1 { get; init; }

    public uint ResourceCount { get; init; }

    public uint Zero2 { get; init; }

    public uint RangeRecordCount { get; init; }
}

public sealed class XppDescriptorManifest
{
    public int Index { get; init; }

    public uint Tag { get; init; }

    public string TagHex { get; init; } = string.Empty;

    public uint ByteLength { get; init; }

    public uint LogicalOffset { get; init; }

    public uint Reserved0 { get; init; }

    public uint Reserved1 { get; init; }

    public uint FirstResourceIndex { get; init; }

    public uint ResourceCount { get; init; }
}

public sealed class XppResourceManifest
{
    public int Index { get; init; }

    public uint TypeAndFlags { get; init; }

    public string TypeAndFlagsHex { get; init; } = string.Empty;

    public uint ByteLength { get; init; }

    public uint LogicalOffset { get; init; }

    public uint Reserved { get; init; }

    public bool IsStoredRange { get; init; }

    public string? DataFile { get; init; }

    public string? Sha256 { get; init; }
}

public sealed class XppRangeRecordManifest
{
    public int Index { get; init; }

    public uint FirstIndex { get; init; }

    public uint Count { get; init; }

    public uint Flags { get; init; }

    public string FlagsHex { get; init; } = string.Empty;

    public uint Reserved { get; init; }

    public uint? Sentinel { get; init; }
}

public sealed class XppMetadataChunkManifest
{
    public int ResourceIndex { get; init; }

    public int ChunkIndex { get; init; }

    public string Tag { get; init; } = string.Empty;

    public uint ByteLength { get; init; }

    public uint ResourceRelativeOffset { get; init; }

    public string DataFile { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;
}

public sealed class XppTextureManifest
{
    public int Index { get; init; }

    public uint DescriptorLogicalOffset { get; init; }

    public uint DataLogicalOffset { get; init; }

    public uint DataLength { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int MipCount { get; init; }

    public byte RsxFormat { get; init; }

    public string Format { get; init; } = string.Empty;

    public string DataFile { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;
}
