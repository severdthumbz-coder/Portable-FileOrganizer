# FileOrganizer v5.0 - Build 1.4.7 Changelog

**Release Date:** July 9, 2026
**Build Type:** Architecture Refactor — Step 4d, part 1 (Statistics ViewModel)

---

## Overview
Step 4d is the finale of the refactor — Statistics + Operations. Because Operations owns the queue, undo, and resume (the app's core), 4d is split into two builds so each can be verified independently:

- **This build (part 1):** extract `StatisticsViewModel` — the low-risk read-model.
- **Next build (part 2):** extract `OperationsViewModel` — the queue/undo/resume pipeline.

**No behavior change.**

---

## What changed

### New: `StatisticsViewModel`
Owns everything the Statistics tab shows:
- operation counters: `TotalFilesOrganized`, `TotalOperations`, `DataProcessedGB`,
- duplicate figures: `DuplicateGroupsFound`, `WastedSpaceGB` (also written by the Duplicates tab),
- verification counters: `TotalFilesVerified`, `VerificationPassed`, `VerificationFailed`, `VerificationRetried`,
- the computed `VerificationSuccessRate`,
- the verification log list and `RecentVerificationFailures`,
- the Refresh command.

### Statistics as a read-model, done properly
The Statistics tab doesn't *do* anything — it displays totals that operational code produces. Those ~29 write sites live inside `LiveMove`, `LiveCopy`, resume, the scan/quick-scan paths, and duplicate detection.

Rather than move that operational code (that's part 2), the writes now route through a broadened **`IStatsSink`**, which `StatisticsViewModel` implements. `MainViewModel` holds a `_stats` reference to it, so each site changed from e.g.:
```csharp
TotalFilesOrganized += opResult.SuccessCount;
TotalOperations++;
DataProcessedGB += ...;
TotalFilesVerified += ...;   // + 3 more verification lines
```
to one call:
```csharp
_stats.RecordOperation(successCount, dataGB, filesVerified, passed, failed, retried);
```
Standalone `TotalOperations++` sites became `_stats.IncrementOperations()`.

### IStatsSink broadened
Previously (Build 1.4.6) it carried just the duplicate figures + `IncrementOperations`. It now covers every statistic the operational code writes, plus `RecordOperation(...)` and `LogVerification(...)`. `DuplicatesViewModel` is unaffected — it uses the same subset as before, now served by `StatisticsViewModel` instead of `MainViewModel`.

### Construction order
`StatisticsViewModel` is created before `DuplicatesViewModel` and passed to it as the `IStatsSink`, so the duplicate figures shown on both tabs share one source of truth.

---

## Files Added
- `ViewModels/StatisticsViewModel.cs`

## Files Modified
- `ViewModels/IStatsSink.cs` — broadened to cover all operational statistic writes
- `ViewModels/MainViewModel.cs` — 11 stat properties, 8 fields, the Refresh command, and the dead `LogVerification`/`RefreshStatistics` removed; ~29 write sites routed through `_stats`; no longer implements `IStatsSink` (StatisticsViewModel does)
- `MainWindow.xaml` — Statistics view DataContext set to `StatisticsVM`
- `Views/HelpTabView.xaml`, `SplashScreen.xaml`, `FileOrganizer.csproj` — changelog + version

---

## Testing notes
**Not compiled here.** After building, verify the Statistics tab reflects real activity:

1. **Live Move / Live Copy** — run each; confirm Total Files Organized, Total Operations, and Data Processed increase by the right amounts, and the completion banner still shows the GB figure.
2. **Verification** — with verification enabled, confirm Files Verified / Passed / Failed / Retried and the Success Rate update.
3. **Duplicate detection** — confirm Duplicate Groups Found and Wasted Space appear on **both** the Duplicates and Statistics tabs (shared source).
4. **Resume** — resume an interrupted operation; confirm the organized/data counters still advance.
5. **Refresh** — the Refresh button on the Statistics tab updates the status bar and recomputes the success rate.
6. **Scans** — run scans/quick scans; confirm the operation count ticks up.

---

## Next step (completes the refactor)
- **Step 4d, part 2:** `OperationsViewModel` — the file queue, scan, live move/copy, dry run, undo, and resume. The largest and most careful extraction; it turns `MainViewModel` into a thin coordinator and finishes the strangle of the God object.

---

*End of Build 1.4.7 Changelog*
