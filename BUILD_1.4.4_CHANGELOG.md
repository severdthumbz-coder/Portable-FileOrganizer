# FileOrganizer v5.0 - Build 1.4.4 Changelog

**Release Date:** July 9, 2026
**Build Type:** Architecture Refactor — Step 4a (Exceptions ViewModel)

---

## Overview
Step 4 is "extract the remaining tab ViewModels." Before moving anything, I measured how coupled each remaining tab is to the operational core:

| Tab | References to shared/operational state |
|---|---|
| **Exceptions** | 17 — but the list management is self-contained |
| Statistics | 17 — **but its counters are written from 33 sites** across Operations & Duplicates |
| History | 29 — tangled with `_historyManager` and `ReRunOperation` |
| Duplicates | 86 |
| Operations | 98 |

The measurement changed the plan. Only **Exceptions** is cleanly separable the way Search and Automation were in Steps 2–3. The others are not independent tabs — they are woven into the scan/move pipeline. **Statistics in particular is a read-model**: the Statistics tab only displays counters that Operations and Duplicates code increment (33 write sites). Extracting it means editing that operational code — which is exactly the high-risk work deferred to last.

So this build extracts Exceptions properly and stops there, rather than forcing four entangled extractions into one untestable change.

**No behavior change.**

---

## What changed

### New: `ExceptionsViewModel`
Owns the Exceptions tab: the `ObservableCollection<ExceptionFilter>`, the `AddException` flow (folder/file picker + Exclude/Semi choice), single-item removal, and multi-select removal.

Depends only on:
- `INotificationService` — status messages
- `SessionContext` — to seed the file/folder dialogs from the current source folder

### Shared collection stays reachable
The scan pipeline's `ApplyExceptionFilters` (still in `MainViewModel`) reads the exception list, and config save/load persists it. So `MainViewModel` exposes the child VM's exact collection instance:
```csharp
public ObservableCollection<ExceptionFilter> Exceptions => ExceptionsVM.Exceptions;
```
No copying, no syncing — one instance, two readers.

### Code-behind updated
`ExceptionsTabView.xaml.cs` previously cast its DataContext to `MainViewModel`. It now works against `ExceptionsViewModel` and delegates multi-select removal to a `RemoveSelected(...)` method on the VM (the handler stays in code-behind only because it reads the DataGrid's live selection).

### View re-pointed
```xml
<views:ExceptionsTabView DataContext="{Binding ExceptionsVM}"/>
```

---

## Why not Statistics / Duplicates / History / Operations in this build
These four share state with the scan/move pipeline that cannot be cleanly cut without editing operational code:

- **Statistics** — a pure read-model. Its 11 displayed properties are incremented from 33 sites inside `LiveMove`, `LiveCopy`, scan, and dedup. Extracting it requires routing all 33 writes through the new VM. That is mechanical but touches the most operational code, so it belongs with the Operations work, not before it.
- **Duplicates** (86 refs) and **Operations** (98 refs) own the queue, dedup engine, undo, and resume — the core.
- **History** (29 refs) is tied to `_historyManager` and the `ReRunOperation` flow.

Rushing these into one build would risk a silent regression (e.g. a counter that quietly stops updating) that can't be caught at compile time. They will come out with care — Operations last.

---

## Files Added
- `ViewModels/ExceptionsViewModel.cs`

## Files Modified
- `ViewModels/MainViewModel.cs` — Exceptions collection now delegates to `ExceptionsVM`; add/remove methods and commands removed; `ApplyExceptionFilters` retained for the pipeline
- `Views/ExceptionsTabView.xaml.cs` — works against `ExceptionsViewModel`
- `MainWindow.xaml` — Exceptions view DataContext set to `ExceptionsVM`
- `Views/HelpTabView.xaml`, `SplashScreen.xaml`, `FileOrganizer.csproj` — changelog + version

---

## Testing notes
**Not compiled here.** After building, verify on the **Exceptions tab**:
1. **Add** — click Add Exception, pick a folder then a file, choose Exclude vs Semi-Exclude; the row appears and the status bar updates.
2. **Remove (single)** — the per-row remove path.
3. **Remove Selected** — multi-select several rows and remove them; status shows the count.
4. **Filtering still works** — add an exclude exception, then run a scan/dry-run and confirm the excluded path is filtered from the queue (this proves the pipeline still reads the shared collection).
5. **Persistence** — Save Configuration, restart, confirm exceptions reload.

---

## Next steps
- **Step 4b:** Extract `HistoryViewModel` (moderate coupling; `ReRunOperation` will need to reach session state via `SessionContext`).
- **Step 4c:** `StatisticsViewModel` as an explicit stats sink that Operations writes through — done together with, or just before, the Operations extraction since that is where the 33 writes live.
- **Step 4d:** `DuplicatesViewModel`.
- **Step 4e (last):** `OperationsViewModel` — the queue, undo, resume, and the live move/copy pipeline.

---

*End of Build 1.4.4 Changelog*
