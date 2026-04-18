# Trackmania2020Toolbox

*Disclaimer: This code was mostly written by an AI and has not been thoroughly checked by a human.*

A command-line utility for downloading Trackmania 2020 maps and batch updating `.Map.Gbx` files. It can download maps from various campaigns and automatically apply fixes to `TitleId` and `MapType`.

## Features

- **Downloader**:
  - Weekly Shorts & Weekly Grands
  - Seasonal Campaigns
  - Club Campaigns
  - Track of the Day
- **Player**:
  - Launch Trackmania with the first selected map using the `--play` flag.
  - Automatically handles downloaded maps or local files/folders.
  - Requires setting the game executable path once via `--set-game-path`.
- **Fixer**:
  - Recursively processes `.Map.Gbx` files.
  - Updates `TitleId` from `OrbitalDev@falguiere` to `TMStadium` by default.
  - Converts `MapType` from `TM_Platform` to `TM_Race` by default.
  - Runs automatically on downloaded maps.
  - Batch mode for existing folders.
  - Dry-run mode to preview changes.

## Requirements

This script relies on several libraries referenced at the top of the file:
```csharp
#:package GBX.NET@2.*
#:package GBX.NET.LZO@2.*
#:package ManiaAPI.TrackmaniaIO@2.*
#:package TmEssentials@2.*
```

## Usage

Run from the repository root:

```bash
# Show help
dotnet run --project src/Trackmania2020Toolbox.csproj -- --help

# Download Weekly Shorts (week 68)
dotnet run --project src/Trackmania2020Toolbox.csproj -- --weekly-shorts 68

# Download TOTD for Oct 2024 (days 1 to 5)
dotnet run --project src/Trackmania2020Toolbox.csproj -- --totd 2024-10 1-5

# Set the game path (required for --play)
dotnet run --project src/Trackmania2020Toolbox.csproj -- --set-game-path "C:\Path\To\Trackmania.exe"

# Download latest Weekly Shorts and play them immediately
dotnet run --project src/Trackmania2020Toolbox.csproj -- --weekly-shorts --play

# Play specific local maps
dotnet run --project src/Trackmania2020Toolbox.csproj -- --play "C:\Maps\Map1.Map.Gbx" "C:\Maps\Map2.Map.Gbx"

# Batch fix an existing folder without changing TitleId
dotnet run --project src/Trackmania2020Toolbox.csproj -- --folder "C:\Maps" --skip-title-update

# Dry-run batch fix
dotnet run --project src/Trackmania2020Toolbox.csproj -- --dry-run
```

### Options

#### Download Options
- `--weekly-shorts <weeks>`: (e.g., "68, 70-72")
- `--weekly-grands <weeks>`: (e.g., "65")
- `--seasonal <name>`: (e.g., "Winter 2024")
- `--club-campaign <search|id>`: (e.g., "123/456")
- `--totd <YYYY-MM> [days]`: (e.g., "2024-10" "1-5")

#### Play Options
- `--play`: Launch Trackmania with the first map found (requires game running).
- `--set-game-path <path>`: Set the path to `Trackmania.exe` in `config.toml`.

#### Fixer Options
- `--folder`, `-f <path>`: Folder for batch fixing (default: `Documents\Trackmania2020\Maps\Toolbox`)
- `--skip-title-update`: Do not update `TitleId`
- `--skip-maptype-convert`: Do not convert `MapType`
- `--dry-run`: Show changes without saving
- `--help`, `-h`: Show help message
