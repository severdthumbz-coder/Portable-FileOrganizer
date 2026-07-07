# FileOrganizer v5.0 - Build 1.2.12 Changelog

**Release Date:** July 7, 2026
**Build Type:** Tooling, Documentation & CI Foundation

---

## Overview
Build 1.2.12 focuses on developer tooling and documentation rather than runtime features. It fixes the portable build script, brings the in-app changelog current, adds a Recommended Workflow guide to the Help tab, and consolidates the GitHub Actions CI foundation.

---

## Changes

### Help tab — Recommended Workflow
A new "Recommended Workflow" section was added to the Help tab, laying out a safe, repeatable process: set up exceptions first, confirm configuration (including manual storage override when needed), scan and review the queue, always Dry Run before a Live Move, check duplicates, then verify and undo if needed.

### Help tab — Changelog catch-up
The in-app changelog was stale at Build 1.2.9. Entries for Builds 1.2.10, 1.2.11, and 1.2.12 were added (newest-first) so the Help tab matches the shipped code.

### Build script (build-portable.bat)
- **Fixed false failure:** the script previously printed both "SUCCESS" and "BUILD FAILED" on a good build. Root cause was `%ERRORLEVEL%` being evaluated at parse time and reset by an intervening `echo`. The script now uses delayed expansion and captures the publish exit code immediately.
- **Exe existence check:** even on a zero exit code, the script now verifies the published exe actually exists before reporting success.
- **Versioned output:** the script reads `<Version>` from `FileOrganizer.csproj` and creates a version-stamped copy, e.g. `PortableFileOrganizer_v5.0.2.12.exe`, so the version stays in sync automatically.
- **Open publish folder:** after a successful build, pressing a key opens the publish folder in Explorer.
- **Escaped parentheses:** fixed unescaped `()` inside an `if` block that could break block parsing.

### Versioning
All version fields bumped to Build 1.2.12: `AssemblyVersion`, `FileVersion`, `Version`, `InformationalVersion` (csproj), `VersionInfo` (MainViewModel), and the version strings in MainWindow.xaml and SplashScreen.xaml.

---

## Wiring Audit (no code changes required)
A full audit of command and event wiring was performed:
- All 25 ViewModel commands are declared, initialized, and bound where UI-triggered.
- Every XAML `Click` handler has a matching code-behind method.
- All services are present and referenced.

**Note (optional cleanup):** `RemoveSourceFolderCommand` and `RemoveExceptionCommand` are implemented in the ViewModel but not bound in XAML — removal is handled instead by the code-behind handlers `RemoveSelectedSourceFolders_Click` and `RemoveSelectedExceptions_Click`, which operate on the DataGrid selection. The feature works; the two commands are redundant dead code and could be removed in a future cleanup build.

---

## Files Modified
- `build-portable.bat` — rewritten with correct failure detection, versioned output, and open-folder step
- `MainWindow.xaml` — Recommended Workflow section; changelog entries for 1.2.10–1.2.12; version strings
- `SplashScreen.xaml` — version string
- `ViewModels/MainViewModel.cs` — `VersionInfo` string
- `FileOrganizer.csproj` — version fields
- `BUILD_1.2.12_CHANGELOG.md` — this file

---

## Suggested Future Enhancements (from audit)
- Remove the two redundant Remove* commands (dead code)
- Real-time performance monitoring during batch ops (transfer speed, ETA, files/sec)
- Advanced file filtering (size, date, regex)
- Save/load file queues
- Auto-detect the publish framework folder in the build script

---

*End of Build 1.2.12 Changelog*
