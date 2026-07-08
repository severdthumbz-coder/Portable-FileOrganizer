# FileOrganizer v5.0 - Build 1.3.0 Changelog

**Release Date:** July 8, 2026
**Build Type:** Major Feature — Automation (Tier 1)

---

## Overview
Build 1.3.0 adds an Automation subsystem that turns FileOrganizer from a run-on-demand utility into a set-and-forget organizer. It introduces a rule engine, real-time folder watching, and scheduled sweeps — the three "table-stakes" automation features common to competing organizers, built on top of FileOrganizer's existing safe transfer engine.

---

## New: Automation tab

### Rule engine
Define rules that route files to destinations. Each rule has:
- One or more **conditions**: Extension is, Name contains, Name matches regex, Size greater/less than (bytes), Modified before/after.
- A **match mode**: All (AND) or Any (OR).
- An **operation**: Move or Copy.
- A **destination folder** and **conflict handling** (skip / overwrite / overwrite-if-newer / keep-both).

Rules evaluate **top-down; the first matching rule wins**, so you can place specific rules above general ones.

### Real-time watching
Watch one or more folders. When a new file appears and finishes writing (the watcher waits until the file is no longer locked), it's evaluated against your rules and organized automatically. Uses the safe CustomFast engine (copy-verify, and for a move only deletes the source after a verified copy).

### Scheduled sweeps
Run a rule-based sweep of the watched folders on a recurring interval (default 60 minutes), with an option to run one immediately on start and a "Run Sweep Now" button. Sweeps process files already present, complementing the watcher's react-to-new-files behavior.

### Activity log
A live, timestamped log of every automated action (moved/copied/skipped/failed) shown directly in the tab.

---

## Architecture notes
- **RuleEngine** (`Services/RuleEngine.cs`) — evaluates a file against rules, returns the winning rule and destination.
- **FolderWatcherService** (`Services/FolderWatcherService.cs`) — `FileSystemWatcher` wrapper with file-settle detection; hands matches to the move engine.
- **ScheduledSortService** (`Services/ScheduledSortService.cs`) — `DispatcherTimer`-based recurring sweeps.
- **MoveEngine.OrganizeFileAsync** — new single-file executor reused by both triggers, applying operation + conflict handling via the safe CustomFast engine.
- **OrganizationRule / RuleCondition** (`Models/OrganizationRule.cs`) — the rule data model, persisted in config.

Automation deliberately uses the CustomFast engine (not TeraCopy/FastCopy) for its silent, verified, headless operation.

---

## Files Added
- `Models/OrganizationRule.cs`
- `Services/RuleEngine.cs`
- `Services/FolderWatcherService.cs`
- `Services/ScheduledSortService.cs`
- `Converters/NullToVisibilityConverter.cs`

## Files Modified
- `Models/Config.cs` — Rules, WatchFolders, Watch/Schedule settings (persisted)
- `Services/MoveEngine.cs` — `OrganizeFileAsync` single-file executor + unique-name helper
- `ViewModels/MainViewModel.cs` — automation collections, properties, commands, save/load
- `MainWindow.xaml` — new Automation tab, converter registration, changelog, version
- `SplashScreen.xaml`, `FileOrganizer.csproj` — version (5.0.3.0 / Build 1.3.0)

---

## Known limitations & testing notes
- **Not compiled/tested here.** This build needs a local compile pass. Test with a small folder first.
- The rules grid's computed columns (Match summary, Operation) do not live-refresh while you edit a rule, because `OrganizationRule` isn't `INotifyPropertyChanged`; values are correct after reselecting or reloading. A future build can add change notification.
- The condition-type dropdown in the conditions grid uses a `DataGridComboBoxColumn` bound to an enum list — verify it populates correctly in your environment.
- Watching uses a file-settle check (waits for exclusive read access) to avoid grabbing partially-written files; very large files still writing may take a few seconds to be picked up.

---

## Suggested next steps (from the roadmap)
- Make `OrganizationRule` implement `INotifyPropertyChanged` for live grid updates.
- Tier 2: real-time performance monitoring during batch operations.
- Content-aware rules (read text inside PDFs/Office docs).

---

*End of Build 1.3.0 Changelog*
