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
