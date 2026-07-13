# FileOrganizer v5.0 - Build 1.4.8 Changelog

**Release Date:** July 9, 2026
**Build Type:** Architecture Refactor — Step 4d, part 2 (Operations ViewModel) — **Refactor Complete**

---

## Overview
The finale. This build extracts the **Operations** pipeline — the file queue, scan, dry run, live move/copy, undo, and crash-resume — into `OperationsViewModel`. It's the largest and most delicate extraction of the whole refactor, because Operations is the core of the application and owns the code where a silent break would actually cost data.

With it done, **`MainViewModel` has gone from 3,364 lines to ~1,120** — a thin coordinator that wires the tab ViewModels together and holds the Configuration-tab settings.

**No behavior change.**

---

## What changed

### New: `OperationsViewModel`
Owns the entire operational surface:
- the `FileQueue` and its counters (pending / moved / failed),
- **scan** (full + quick), with exception filtering,
- **dry run** preview,
- **live move** and **live copy**, with the live performance monitor (speed / ETA / throughput / current file),
- **undo** of the last move,
- **crash-resume**: periodic resume-state saves during long operations, and the startup recovery dialog (resume / undo / discard),
- the progress bar and last-operation duration.

### Dependencies — all narrow, none on MainViewModel
Every dependency Operations needs was already given a home during the earlier steps:

| Needs | Gets |
|---|---|
| status + banner | `INotificationService` |
| source/dest folders, operation mode | `SessionContext` |
| record operations in history | `HistoryViewModel` |
| update statistics totals | `IStatsSink` (StatisticsViewModel) |
| start/failure toasts | `ToastNotificationService` |
| scan the filesystem | `FileScanner` |
| crash-resume persistence | `ResumeStateManager` |
| Configuration-tab settings + exception filter | `IOperationsSettingsProvider` (new) |

### New: `IOperationsSettingsProvider`
The Operations pipeline reads a handful of Configuration-tab settings when it builds a `MoveEngine` or previews destinations: `SelectedScanMode`, `StructureMode`, `ConflictResolution`, and the exception filter. Those settings still live on `MainViewModel` (they belong to the Configuration tab, which remains). This interface exposes them read-only to Operations, so `OperationsViewModel` never depends on `MainViewModel`.

### Startup wiring
The crash-resume check (`CheckForIncompleteOperation`) moved into `OperationsViewModel`. `MainViewModel` keeps a one-line forwarder because `App.xaml.cs` calls it at startup.

### Shell bindings re-pointed
The status bar's duration and the main progress bar live in the `MainWindow` shell. They were bound to `MainViewModel`; they now bind to `OperationsVM.LastOperationDuration` and `OperationsVM.ProgressValue`. The `StatusMessage` and `VersionInfo` bindings stay on `MainViewModel` (they're genuinely app-wide).

---

## Bugs caught during extraction (all pre-CI)
- **Fabricated API.** My first draft of `UndoFromResume` invented a `ResumeStateManager.GetProcessedEntries(...)` method that doesn't exist, and stubbed the logic. Replaced with the real implementation (which walks `state.CompletedFiles` against `state.RemainingQueue`), verified against the actual `ResumeState` model.
- **Truncated catch block.** An edit left `UndoFromResume`'s catch block missing its banner call and two closing braces. Caught by a brace-depth structural check before packaging (each method must start at class depth).
- **Orphaned notifications.** `MainViewModel.OperationMode`'s setter still raised `ShowLiveMoveButton`/`ShowLiveCopyButton` after they moved; `OperationsViewModel` now derives those from a `SessionContext.OperationMode` subscription instead.
- **Construction order.** `DuplicatesViewModel`'s progress/duration callbacks target `OperationsVM`, so `OperationsVM` is now constructed first.

Every service method, model member, and enum value used by `OperationsViewModel` was checked against its definition (`ProcessQueueAsync`, `OperationProgress`, `OperationResult`, `QueueEntry`, `ResumeState`, and the `FileOperationMode` / `DestinationStructureMode` / `FileConflictResolution` / `ScanMode` enums).

---

## The refactor, end to end
`MainViewModel.cs` line count across the strangle:

| Build | Step | Lines |
|---|---|---|
| 1.4.1 (start) | tabs → UserControls | 3,364 |
| 1.4.2 | Search + Automation VMs | 2,948 |
| 1.4.3 | NotificationService + SessionContext | 2,970 |
| 1.4.4 | Exceptions VM | 2,862 |
| 1.4.5 | History VM | 2,810 |
| 1.4.6 | Duplicates VM | 2,301 |
| 1.4.7 | Statistics VM | 2,187 |
| **1.4.8** | **Operations VM** | **~1,120** |

The God object is gone. Nine tabs, nine ViewModels (Configuration's settings remain on the coordinator), all talking through small, testable interfaces.

---

## Testing notes
**Not compiled here.** This is the highest-stakes build — exercise the full pipeline:

1. **Scan** — full scan and quick scan; queue populates, counts correct, exceptions still filtered.
2. **Dry run** — preview counts match the queue; structure/conflict settings reflected.
3. **Live move** — moves files; progress bar + performance monitor animate; status + banner correct; History + Statistics update.
4. **Live copy** — same, originals retained.
5. **Undo** — after a move, undo restores files to source.
6. **Resume** — start a large move, kill the app mid-operation, relaunch: the resume dialog should offer Resume / Undo / Discard, and each should behave correctly. **This is the most important path to test.**
7. **Clear queue** — empties the queue and resets counters.
8. **Move/Copy toggle** — the correct Live button shows (driven by SessionContext now).
9. **Duplicates** — its progress/duration still drive the shared progress bar and duration text.

---

## Next (optional)
The only tab still served directly by `MainViewModel` is **Configuration** (folder pickers, engine detection, scan mode, verification settings, space analysis). It could become `ConfigurationViewModel` to make `MainViewModel` a pure coordinator — but that's polish, not necessity. The God object is already dismantled.

---

*End of Build 1.4.8 Changelog — refactor complete.*
