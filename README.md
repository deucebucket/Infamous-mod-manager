# InFamous Mod Manager

Cross-platform desktop mod manager for the PlayStation 3 versions of **InFamous**, **InFamous 2**, and **InFamous: Festival of Blood** running through RPCS3.

The application is built with Avalonia UI, .NET, and CommunityToolkit.Mvvm.

## Current features

- Game selection for InFamous 1, InFamous 2, and InFamous: Festival of Blood.
- English and Russian interface languages.
- Native folder picker for locating the RPCS3 `dev_hdd0/game` directory.
- Detection and validation of supported game editions:
  - `NPUA80480` — InFamous 1 PSN edition (loose XPPS workflow);
  - `BCUS98119` — InFamous 1 Blu-ray / InFamous Collection edition (packed PSARC workflow);
  - `NPUA80638` — InFamous 2;
  - `NPEA00322` — InFamous: Festival of Blood.
- Independent saved paths, backup directories, and installation history for each detected title ID.
- Explicit ambiguity and incomplete-installation errors instead of guessing when more than one edition is present.
- Extraction of `install1` and `install2` PSARC archives into the selected game's `USRDIR` directory.
- PSARC extraction with zlib and uncompressed-block support.
- Automatic backup of original `.psarc_s` archives after successful extraction.
- Lossless extraction of individual `.xpp` and `.xpps` PACK containers:
  - extracts the original header, resource table, and payload;
  - extracts indexed resource views and framed metadata chunks;
  - detects embedded DXT1/DXT3/DXT5 textures and writes ready-to-use DDS files;
  - writes a JSON manifest with offsets, sizes, types, ranges, and SHA-256 hashes;
  - supports InFamous 1 XPP, InFamous 2/Festival of Blood XPPS, and empty archive stubs.
- Native `.xpps` mod-file picker.
- Mod installation into the unpacked game directory:
  - normalizes mod file names to lowercase with underscores;
  - finds the matching original `.xpps` file;
  - backs up the original file before installing the mod;
  - reports successful and failed operations in the UI.
- Audited packed-profile installation for `BCUS98119`:
  - accepts a flat `infamous1.psarc_s`/`infamous2.psarc_s` pair or `install1`/`install2` subdirectories;
  - verifies both archives against the retail manifest order and block layout;
  - creates and hash-checks a protected retail backup before the first installation;
  - stages and hash-checks the complete pair before swapping either live archive;
  - rolls both archives back if activation fails and restores the verified retail pair on request;
  - refuses archive changes while RPCS3 is running.

Backups and `user-data.json` are stored beside the application executable. Backups are grouped by detected game ID in the `backup` directory. The verified `BCUS98119` retail pair is stored under `backup/BCUS98119/packed-retail` and is never modified in place.

## Planned features

- Rebuild `.xpp` and `.xpps` archives from extraction manifests.
- Display the list of installed mods and modified files.
- Restore individual original files or all backups.
- Better progress reporting and cancellation for long extraction operations.
- Additional validation and recovery options for incomplete installations.
- More interface languages.
- Settings for configurable RPCS3 and backup directories.

## Development

Requirements:

- .NET 10 SDK

Build the project:

```bash
dotnet build
```

Run the application:

```bash
dotnet run
```

## Third-party notices

The PSARC extraction implementation is based on the extraction approach from the local `PSARC-Tool` project. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for license information.
