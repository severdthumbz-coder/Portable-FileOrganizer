# Portable File Organizer

A portable Windows desktop utility for scanning, categorizing, and organizing large numbers of files. It sorts files into category folders, detects duplicates, and moves or copies them with an adaptive engine that scales its throughput to the underlying storage (NVMe / SSD / HDD). No installation required — publish it as a single self-contained `.exe` and run it anywhere.

**Current version:** v5.0 — Build 1.2.11
**Platform:** Windows 10 (1809+) / Windows 11
**Framework:** .NET 9 (WPF, MVVM)

---

## Features

**Scanning & categorization**
- Initial (full) scan and Quick scan modes
- Automatic file categorization (Programs, Code, Other, and more) based on type
- File queue with per-file status, category, size, verification state, and source path

**Duplicate detection**
- Full scan using SHA-256 hashing (most accurate)
- Quick scan by size only (faster)
- Grouped results with wasted-space totals and smart auto-selection strategies

**File operations**
- Move and Copy with conflict handling (e.g. overwrite-if-newer)
- Dry Run preview that reports totals before anything is touched
- Structure preservation options
- Undo for the last Move operation, plus undo of an interrupted/resumed operation
- Resume support for operations interrupted partway through

**Adaptive performance**
- Detects storage type and scales worker threads accordingly (NVMe / SSD / HDD)
- Manual drive-type override when auto-detection isn't right for your setup
- Configurable retry attempts and retry delay for error recovery

**UI**
- In-app banner notification system (non-blocking, auto-dismiss) for operation results, replacing blocking pop-ups
- Modal dialogs preserved only where a deliberate decision is required (Undo confirmation, Dry Run preview)
- Light and dark themes
- Tabs: Configuration, Operations, Statistics, Exceptions, History, Duplicates, Help

---

## Getting started

### Prerequisites
- Windows 10 version 1809 (build 17763) or later
- [.NET 9 SDK](https://dotnet.microsoft.com/download) to build from source

### Clone
```bash
git clone https://github.com/<your-username>/FileOrganizer.git
cd FileOrganizer
```

### Run in development
```bash
dotnet restore
dotnet run
```

### Build a portable single-file executable
Use the included script:
```bash
build-portable.bat
```
Or run the publish command directly:
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
The portable executable is written to:
```
bin\Release\net9.0-windows\win-x64\publish\PortableFileOrganizer.exe
```
The self-contained build bundles the .NET runtime (~200 MB) so it runs on target machines with no install step.

---

## Project structure

| Folder | Contents |
|--------|----------|
| `Models/` | Data models and enums — `Config`, `DataModels` (queue/history/exception/resume state), `ScanMode`, `CopyEngine`, `VerificationMode`, `Enums` |
| `ViewModels/` | `MainViewModel` — MVVM logic, commands, and UI state |
| `Services/` | Core engines and managers — `FileScanner`, `DuplicateDetector`, `MoveEngine`, `ConfigManager`, `HistoryManager`, `ResumeStateManager`, `AdaptivePerformanceManager`, `SystemCapabilities`, `EngineDetector`, and copy engines (`CustomFastCopyEngine`, `FastCopyEngine`, `TeraCopyEngine`) |
| `Controls/` | `BannerNotification` — the in-app banner control |
| `Converters/` | WPF value converters (visibility, boolean inversion, progress width, expand/collapse) |
| `Commands/` | `RelayCommand` MVVM command implementation |
| `Themes/` | `LightTheme.xaml`, `DarkTheme.xaml` |
| `App.xaml` / `MainWindow.xaml` / `SplashScreen.xaml` | Application entry, main UI, and splash screen |

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Newtonsoft.Json` | 13.0.3 | Configuration and state serialization |
| `System.Management` | 9.0.0 | WMI queries for storage/drive detection |
| `Microsoft.Toolkit.Uwp.Notifications` | 7.1.3 | System notification support |

---

## Versioning

Builds follow `major.minor.patch` with an in-app changelog kept current on the Help tab. The current build is **1.2.11**; recent work focused on the banner notification system (Builds 1.2.9–1.2.11).

> **Note:** keep the four version fields in `FileOrganizer.csproj` (`AssemblyVersion`, `FileVersion`, `Version`, `InformationalVersion`) in sync with each build, along with the `VersionInfo` string in `MainViewModel` and the version text in `MainWindow.xaml` / `SplashScreen.xaml`.

---

## Roadmap

Ideas under consideration for future builds:
- Real-time performance monitoring (live transfer speed, ETA, files/sec) during batch operations
- Advanced file filtering (size, date, regex, name contains/excludes)
- Save/load file queues for analyze-now-execute-later workflows
- Custom user-defined categories and rename patterns
- Statistics dashboard with visual charts

---

## License

_No license specified yet._ Add a `LICENSE` file to declare how others may use this project (for a personal project you can leave it unlicensed, or choose something permissive like MIT).

---

*Portable File Organizer is a personal development project.*
