# FileOrganizer v5.0 - Build 1.4.5 Changelog

**Release Date:** July 9, 2026
**Build Type:** Architecture Refactor — Step 4b (History ViewModel)

---

## Overview
Continues extracting the remaining tab ViewModels. This build moves the **History** tab out of `MainViewModel`, following the same pattern as Exceptions: the tab owns its collection and UI actions, while the operational code that *creates* history entries stays put and goes through a thin forwarder.

**No behavior change.**

---

## What changed

### New: `HistoryViewModel`
Owns:
- the `ObservableCollection<HistoryEntry>` (capped at 50 in the UI),
- on-disk persistence (`HistoryManager`),
- `AddEntry(...)` — records a completed operation,
- `LoadPersisted()` — loads saved history at startup,
- the **Re-run** command.

Depends only on `INotificationService` (status) and `SessionContext` (Re-run repopulates the session folders/mode; `AddEntry` reads them to remember what a Move/Copy acted on).

### Operational code untouched
History entries are created from 16 places across scans, dedup, undo, live move/copy, and resume. Rather than edit all 16, `MainViewModel.AddHistoryEntry(...)` remains as a one-line forwarder to `HistoryVM.AddEntry(...)`. The collection is still reachable for anything that reads it via `History => HistoryVM.History`.

### Bug fixed: storage detection on Re-run
While extracting Re-run, I found a latent issue. `SourceFolder`'s capability-refresh side effect (detecting HDD/SSD/NVMe) lived only in `MainViewModel`'s property setter. A History Re-run that set the source folder through `SessionContext` would have **skipped** that refresh, leaving storage detection stale.

Fix: the capability-refresh side effect moved **into `SessionContext.SourceFolder`**, so *every* writer triggers it — the Configuration tab and Re-run alike. `SessionContext` raises a `SourceFolderChanged` event that `MainViewModel` subscribes to for the dependent UI text (`SystemDetectedDescription`, `ScanModeDescription`).

This makes the earlier delegation (Build 1.4.3) more correct than it was.

---

## Files Added
- `ViewModels/HistoryViewModel.cs`

## Files Modified
- `ViewModels/MainViewModel.cs` — History collection/command/methods removed; `AddHistoryEntry` is now a forwarder; history load delegates to the child VM; `SourceFolder` setter simplified
- `ViewModels/SessionContext.cs` — owns the source-folder capability-refresh side effect and a `SourceFolderChanged` event
- `MainWindow.xaml` — History view DataContext set to `HistoryVM`
- `Views/HelpTabView.xaml`, `SplashScreen.xaml`, `FileOrganizer.csproj` — changelog + version

---

## Testing notes
**Not compiled here.** After building, verify:

1. **History records** — run a scan, a dry run, a live copy; each should appear at the top of the History tab with correct counts and status.
2. **Persistence** — restart the app; prior history should reload.
3. **Re-run** — click Re-run on a Move/Copy entry; source, destination, and mode should repopulate and the status bar should prompt you to scan.
4. **Re-run storage detection (the fix)** — Re-run an entry whose source is on a *different drive type* than the current one, then check the Configuration tab's storage-detection text updated. This is the behavior that would previously have gone stale.
5. **Cap** — confirm the list still trims to 50 entries.

---

## Next steps
- **Step 4c:** `DuplicatesViewModel` (86 refs — owns the dedup engine, groups, selection, delete/move).
- **Step 4d (last):** `StatisticsViewModel` + `OperationsViewModel` together, since the 33 statistics write-sites live inside the Operations pipeline (queue, undo, resume, live move/copy).

---

*End of Build 1.4.5 Changelog*
