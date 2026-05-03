# Trackmania2020Toolbox

*Disclaimer: This code was mostly written by an AI and has not been thoroughly checked by a human.*

A utility for downloading Trackmania 2020 maps and batch updating `.Map.Gbx` files. It features both a modern GUI and a command-line interface.

## Features

- **GUI**: A modern, cross-platform interface built with Avalonia UI.
- **Downloader**:
  - Weekly Shorts & Weekly Grands
  - Seasonal Campaigns
  - Club Campaigns
  - Track of the Day
  - Trackmania Exchange (TMX) maps, packs, and search
- **Medal Exporter**:
  - Export personal records and medals from official seasonal campaigns to CSV.
- **Player**:
  - Launch Trackmania with the selected maps using the `--play` flag (CLI) or the "Play" feature in the GUI.
  - Automatically handles downloaded maps or local files/folders.
- **Fixer**:
  - Recursively processes `.Map.Gbx` files.
  - Updates `TitleId` and converts `MapType` for compatibility.
  - Runs automatically on downloaded maps.
- **NativeAOT Compatible**: Optimized for performance and small binary size.

## Requirements

- .NET 10.0 SDK or higher.

## Downloader Flag Formats

The CLI supports advanced range and precision options for downloading maps:

### Common Keywords
- `latest`: Downloads the most recent content.
- `all`: Downloads everything available in that category.

### Weekly Shorts
Supports week numbers, ranges, and map-level precision using dot notation:
- `--weekly-shorts 4`: Downloads Week 4.
- `--weekly-shorts 6-12`: Downloads Week 6 through 12 inclusive.
- `--weekly-shorts 15.1`: Downloads only Map 1 of Week 15.
- `--weekly-shorts 21.3-23.1`: Downloads from Week 21 Map 3 up to Week 23 Map 1.

### Weekly Grands
Supports week numbers and ranges:
- `--weekly-grands 1, 6, 29-33`

### Seasonal Campaigns
Supports years, seasons, ranges, and map precision:
- `--seasonal 2024`: Downloads all 4 seasons of 2024.
- `--seasonal "Winter 2025"`: Downloads a specific season.
- `--seasonal "Summer 2021.1"`: Downloads Map 1 of Summer 2021.
- `--seasonal "Summer 2022 - Fall 2022"`: Downloads a seasonal range.
- `--seasonal "Winter 2026.24 - Spring 2026.3"`: Downloads a precise range across seasons.

### Track of the Day
Supports years, months, days, and complex ranges. Uses dots, slashes, or dashes as separators:
- `--totd 2024`: Downloads all maps from 2024.
- `--totd 2024.02`: Downloads all maps from February 2024.
- `--totd 2024.02.15`: Downloads a specific day.
- `--totd 2024.02.15-20`: Downloads a range within a month.
- `--totd 2024.02.15-03.10`: Downloads a range across months.
- `--totd 2024-2025`: Downloads all maps from 2024 and 2025.
- `--totd 12.30-01.05`: Downloads a range across year boundaries (automatically handles year rollover).

### Club Campaigns
- `--club-campaign <clubId>`: Downloads ALL campaigns belonging to that club (searches up to 20 pages).
- `--club-campaign <clubId>/<campaignId>`: Downloads a specific campaign.
- `--club-campaign <search_term>`: Searches for a campaign by name (searches up to 20 pages).

### Trackmania Exchange (TMX)
- `--tmx <ids|urls>`: Download maps from TMX (comma-separated).
- `--tmx-pack <ids|urls>`: Download map packs from TMX.
- `--tmx-search <name>`: Search for maps by name.
- `--tmx-author <name>`: Search for maps by author.
- `--tmx-random`: Download a random map from TMX.

### Medal Exporter
- `--export-campaign-medals <PlayerID> [campaign_filter]`: Export seasonal campaign medals to a CSV file.
- `--medals-output <path>`: Custom output path for the medals CSV file (default: `medals.csv`).

## Project Structure

- `Trackmania2020Toolbox.Core`: Shared logic and API wrappers.
- `Trackmania2020Toolbox.CLI`: Command-line interface.
- `Trackmania2020Toolbox.Desktop`: Avalonia UI desktop application.

## Getting Started

### Build and Test

```bash
# Build the solution
dotnet build

# Run unit tests
dotnet test
```

### Run the GUI

```bash
dotnet run --project src/Trackmania2020Toolbox.Desktop/Trackmania2020Toolbox.Desktop.csproj
```

### Run the CLI

```bash
dotnet run --project src/Trackmania2020Toolbox.CLI/Trackmania2020Toolbox.CLI.csproj -- --help
```

### NativeAOT Publishing

To publish a standalone binary:

**Linux:**
```bash
dotnet publish src/Trackmania2020Toolbox.Desktop/Trackmania2020Toolbox.Desktop.csproj -c Release -r linux-x64 /p:PublishAot=true /p:InvariantGlobalization=true
```

**Windows:**
```bash
dotnet publish src/Trackmania2020Toolbox.Desktop/Trackmania2020Toolbox.Desktop.csproj -c Release -r win-x64 /p:PublishAot=true /p:InvariantGlobalization=true
```
