using System.Collections.Generic;

namespace InfamousModManager.Models;

public sealed record PsarcArchiveInfo(
    string Path,
    long Length,
    string Sha256,
    uint BlockSize,
    IReadOnlyList<string> Entries);
