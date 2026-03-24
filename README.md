# Trackmania2020MapFixer

This script is used to batch process Trackmania `.Map.Gbx` files in a specified folder (and its subdirectories). It parses each map file, checks its `TitleId`, and if the `TitleId` matches a specific value (`OrbitalDev@falguiere`), it changes it to `TMStadium` and overwrites the original file in-place.

## Features

- Recursively processes all `.Map.Gbx` files in a given directory.
- Continues processing even if an individual map file fails to parse.
- Tracks and displays a summary of the analysis at the end:
  - Total number of files processed successfully.
  - Number of files that had their `TitleId` changed.

## Requirements

This script relies on the [GBX.NET](https://github.com/BigBang1112/gbx.net) library to parse and save Trackmania map files. The required packages are referenced at the top of the file:
```csharp
#:package GBX.NET@2.*
#:package GBX.NET.LZO@2.*
```

## Usage

Run from the repository root and point to your map folder if needed.

### command line

```bash
# show help
dotnet run .\Trackmania2020MapFixer.cs -- --help

# default: current working directory
dotnet run .\Trackmania2020MapFixer.cs -- --update-title --convert-platform-maptype

# explicit folder
dotnet run .\Trackmania2020MapFixer.cs -- --folder "C:\Users\Philipp\Documents\Trackmania2020\Maps" --update-title --convert-platform-maptype

# dry-run mode (no writes)
dotnet run .\Trackmania2020MapFixer.cs -- --dry-run --update-title --convert-platform-maptype
```

### options

- `--folder`, `-f <path>`: folder to scan (default: current working directory)
- `--update-title`: change `TitleId` from `OrbitalDev@falguiere` to `TMStadium`
- `--convert-platform-maptype`: change `MapType` from `TrackMania\\TM_Platform` to `TrackMania\\TM_Race`
- `--dry-run`: analyze/print changes without saving files
- `--help`, `-h`: show help

## What it does

- recursively processes all `*.Map.Gbx` files under given folder
- applies selected updates based on flags
- logs per-file success/failure and summary counters

## Required packages

Packages are referenced at the top of the script:

```csharp
#:package GBX.NET@2.*
#:package GBX.NET.LZO@2.*
```

