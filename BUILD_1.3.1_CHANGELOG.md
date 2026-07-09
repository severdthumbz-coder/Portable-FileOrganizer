# FileOrganizer v5.0 - Build 1.3.1 Changelog

**Release Date:** July 8, 2026
**Build Type:** Tier 2 — Performance Monitoring, Resilience & History Re-run

---

## Overview
Build 1.3.1 delivers "Tier 2": it makes FileOrganizer's transfer-engine strengths visible and more robust. A live performance monitor surfaces real-time speed and ETA during transfers, retries now use exponential backoff for network/flaky drives, and past operations can be re-run from History.

---

## New: Live Performance Monitor
During a Live Move or Copy, the Operations tab shows a real-time panel with:
- **Speed** — current transfer rate (auto-scaled B/KB/MB/GB per second)
- **Time Remaining** — ETA based on bytes remaining and current rate
- **Throughput** — files per second
- **Data** — bytes processed / total bytes
- The **current file** being processed

The panel appears automatically when an operation starts and hides when it finishes. Metrics are computed in `MoveEngine.ProcessQueueAsync` and reported through an extended `OperationProgress`.

## New: Resilient Transfers (exponential backoff)
Retry delays now grow exponentially (base × 2^attempt, capped at 30 seconds) instead of a fixed short delay. This smooths over transient failures that are common on network shares and flaky drives, working alongside the existing resume-after-interruption and copy-verify safety.

## New: Re-run from History
Each Move/Copy history entry now records its source and destination folders. A **Re-run** button in the History tab reloads that operation's source, destination, and mode. By design it does **not** auto-execute — it repopulates the settings so you can scan, review (Dry Run), and run again. This keeps re-run consistent with the app's safety-first workflow.

---

## Documentation updates
- **Recommended Workflow** extended with two steps: watching the live performance monitor during transfers, and moving proven workflows into the Automation tab.
- **Features Overview** updated with Automation, Live Performance Monitor, Resilient Transfers, and Re-run from History.

---

## Files Modified
- `Services/MoveEngine.cs` — extended `OperationProgress` with byte/speed/ETA/throughput; computes metrics in the processing loop
- `Services/CustomFastCopyEngine.cs` — exponential backoff for retries
- `Models/DataModels.cs` — `HistoryEntry` now stores `SourceFolder`, `DestinationFolder`, `CanReRun`
- `ViewModels/MainViewModel.cs` — live metric properties + formatting, `UpdatePerformanceMetrics`, `ReRunOperation` command, history capture, monitor show/hide
- `MainWindow.xaml` — performance monitor panel, History Re-run column, Recommended Workflow + Features updates, changelog, version
- `SplashScreen.xaml`, `FileOrganizer.csproj` — version (5.0.3.1 / Build 1.3.1)

---

## Known limitations & testing notes
- **Not compiled/tested here.** Needs a local build pass.
- Speed/ETA are computed per-file (updated as each file completes), so with a few very large files the rate updates in coarse steps rather than continuously. This is accurate but less granular than a byte-streaming meter; a future build could report sub-file progress.
- Skipped files count toward file progress but not transferred bytes, so byte-based ETA is slightly conservative when many files are skipped.
- Re-run intentionally reloads settings only; it does not re-execute automatically.

---

*End of Build 1.3.1 Changelog*
