# InFamous Mod Manager

Cross-platform desktop mod manager for the PlayStation 3 versions of **InFamous**, **InFamous 2**, and **InFamous: Festival of Blood** running through RPCS3.

The application is built with Avalonia UI, .NET, and CommunityToolkit.Mvvm.

## Current features

- Game selection for InFamous 1, InFamous 2, and InFamous: Festival of Blood.
- English and Russian interface languages.
- Native folder picker for locating the RPCS3 `dev_hdd0/game` directory.
- Validation of the expected game folders:
  - `NPUA80480` — InFamous 1;
  - `NPUA80638` — InFamous 2;
  - `NPEA00322` — InFamous: Festival of Blood.
- Extraction of `install1` and `install2` PSARC archives into the selected game's `USRDIR` directory.
- PSARC extraction with zlib and uncompressed-block support.
- Automatic backup of original `.psarc_s` archives after successful extraction.
- Native `.xpps` mod-file picker.
- Mod installation into the unpacked game directory:
  - normalizes mod file names to lowercase with underscores;
  - finds the matching original `.xpps` file;
  - backs up the original file before installing the mod;
  - reports successful and failed operations in the UI.

Backups and `user-data.json` are stored beside the application executable. Backups are grouped by game ID in the `backup` directory.

## Planned features

- Extract individual original `.xpps` files or folders into a user-selected output directory.
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
