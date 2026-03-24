# Trackmania2020MapFixer

A command-line utility for batch updating Trackmania `.Map.Gbx` files in a directory tree. It can update title IDs and map types on matching maps, with optional dry-run mode.

## Features

- Recursively processes all `.Map.Gbx` files in a given directory.
- Supports changing `TitleId` and `MapType` according to flags.
- Keeps scanning after parse failures and reports file-level errors.
- Shows summary counters at end:
  - Total files scanned
  - Files modified


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
dotnet run .\Trackmania2020MapFixer.cs -- --folder "C:\Users\Username\Documents\Trackmania2020\Maps" --update-title --convert-platform-maptype

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

