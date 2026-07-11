# FileOrganizer v5.0 - Build 1.4.6 Changelog

**Release Date:** July 9, 2026
**Build Type:** Architecture Refactor — Step 4c (Duplicates ViewModel)

---

## Overview
Extracts the **Duplicates** tab — the largest and most dependency-heavy tab — into its own ViewModel. `MainViewModel` drops from 2,810 to ~2,300 lines (~500 lines moved).

**No behavior change.**

---

## What changed

### New: `DuplicatesViewModel`
Owns everything the Duplicates tab does:
- duplicate **detection** (quick size-based or full SHA-256),
- the `DuplicateGroups` collection and per-file selection,
- **keep-strategy** auto-selection (Keep Newest / Oldest / Shortest Path / Longest Path),
- selection statistics (files selected, space to free),
- **delete** to Recycle Bin (with the "can't delete every copy in a group" guard),
- **move** duplicates to a chosen folder,
- **export** the duplicate list to CSV,
- clear selection and expand/collapse groups.

### The coupling, and how it was handled
Duplicates was the most entangled tab so far (86 references). It legitimately needs several shared things, so rather than hand it a reference to `MainViewModel`, it receives narrow dependencies:

| Needs | Gets |
|---|---|
| status line + banner | `INotificationService` |
| record operations in history | `HistoryViewModel` |
| the source folder | `SessionContext` |
| update the Statistics totals | `IStatsSink` (new) |
| start/failure toasts | `ToastNotificationService` |
| the progress bar | `Action<double>` callback |
| "last operation" duration text | `Action<string>` callback + `FormatDuration` |
| the selected scan mode | `Func<ScanMode>` accessor |

### New: `IStatsSink`
The Duplicates tab displays *and* contributes two figures that also appear on the Statistics tab: **groups found** and **wasted space**. These are conceptually duplicate-detection results, so `DuplicatesViewModel` now owns them as real properties — and their setters push the values to `IStatsSink`, which `MainViewModel` implements, so the Statistics tab stays in sync.

This interface is the seam the upcoming Statistics/Operations step (4d) will build on: the statistics counters remain on `MainViewModel` for now because they are written from ~30 other places inside the Operations pipeline.

### Why progress/duration/scan-mode are callbacks, not owned
The progress bar, the "last operation duration" text, and the scan-mode selector are shared window chrome and scan configuration used by Operations too. The Duplicates tab shouldn't own them, so it writes into them through small callbacks supplied at construction. This keeps the extraction honest — no shared UI was quietly duplicated.

---

## Files Added
- `ViewModels/DuplicatesViewModel.cs`
- `ViewModels/IStatsSink.cs`

## Files Modified
- `ViewModels/MainViewModel.cs` — duplicate state, 6 commands, and 9 methods removed; implements `IStatsSink`; constructs and exposes `DuplicatesVM`; `DuplicateGroupsFound` / `WastedSpaceGB` retained as statistics counters
- `MainWindow.xaml` — Duplicates view DataContext set to `DuplicatesVM`
- `Views/HelpTabView.xaml`, `SplashScreen.xaml`, `FileOrganizer.csproj` — changelog + version

---

## Testing notes
**Not compiled here.** After building, exercise the Duplicates tab thoroughly:

1. **Detect** — run both Quick Scan (checkbox on) and full scan; confirm groups appear and the "groups found / wasted space" figures show both on the Duplicates tab **and** the Statistics tab.
2. **Keep strategy** — pick each of Keep Newest/Oldest/Shortest/Longest; confirm one file per group is marked keep and the rest selected, and the "selected for deletion" figures update.
3. **Delete** — select some, delete; confirm files go to the Recycle Bin, groups collapse/disappear correctly, and the guard blocks deleting *all* copies in a group.
4. **Move** — move selected duplicates to a folder; confirm name-conflict handling and that it re-scans afterward.
5. **Export** — export to CSV and open it.
6. **Clear selection** — resets selection and strategy.
7. **History** — each detect/delete appears in the History tab.

---

## Next steps
- **Step 4d (final):** `StatisticsViewModel` + `OperationsViewModel` together. Statistics is a read-model whose ~30 write sites live inside the Operations pipeline (scan, live move/copy, undo, resume), so the two must be extracted as a unit. This is the biggest and most careful step — Operations owns the queue, undo, and resume — and completes the refactor.

---

*End of Build 1.4.6 Changelog*
