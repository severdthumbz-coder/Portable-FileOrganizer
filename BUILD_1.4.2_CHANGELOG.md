# FileOrganizer v5.0 - Build 1.4.2 Changelog

**Release Date:** July 9, 2026
**Build Type:** Architecture Refactor — Step 2 (Feature ViewModels)

---

## Overview
Step 2 of dismantling the God object. The **Search** and **Automation** features now have their own ViewModels, chosen first because they were the least coupled to shared state.

**No behavior change.** Every control, command, and setting works exactly as before.

---

## What changed

### New: `SearchViewModel`
Owns the Search tab entirely. It was already well-isolated — it maintained its own `SearchStatus` rather than writing the global status bar, and its only shared-state read was `DestinationFolder` as a fallback search root.

### New: `AutomationViewModel`
Owns rules, folder watching, and scheduled sweeps. This one was genuinely coupled: **23 writes to `StatusMessage`**, five transfer-settings reads, and a `SourceFolders` fallback.

### New: decoupling interfaces
Rather than let the child ViewModels reach back into `MainViewModel`, two narrow interfaces were introduced (a down-payment on Step 3):

| Interface | Purpose |
|---|---|
| `IStatusSink` | `SetStatus(string)` — write the shared status line |
| `ITransferSettingsProvider` | Expose transfer settings + `BuildAutomationConfig()` + folder fallbacks |

`MainViewModel` implements both explicitly. The children receive interfaces, never the concrete class.

### New: `ViewModelBase`
Shared `INotifyPropertyChanged` / `SetProperty` plumbing, identical to what `MainViewModel` already used.

### View binding
`MainWindow.xaml` now sets each tab's DataContext:
```xml
<views:AutomationTabView DataContext="{Binding Automation}"/>
<views:SearchTabView     DataContext="{Binding Search}"/>
```
Because every binding inside those views targets a member that moved to its child ViewModel, **no markup inside the views was edited**.

---

## Moved out of MainViewModel
- 18 commands (13 automation, 5 search)
- 16 properties, 3 `ObservableCollection`s, 2 enum item-source lists
- Watcher/scheduler service fields and all their methods

`ReRunOperation` deliberately **stayed** — it writes `SourceFolder` / `DestinationFolder` / `OperationMode`, which is Operations-tab state, not automation state. The History tab's Re-run button still binds to `MainViewModel`, whose DataContext is unchanged.

Config persistence was rewired to read/write `Automation.Rules`, `Automation.WatchFolders`, and the schedule settings.

---

## Verification performed
- All XAML well-formed; all C# brace-balanced.
- Every binding in `AutomationTabView` / `SearchTabView` maps to a member that exists on its child ViewModel (checked exhaustively, including `RelativeSource AncestorType=DataGrid` command bindings and per-item template bindings).
- Every child-VM command is declared and initialized.
- All `ITransferSettingsProvider` members exist as public properties on `MainViewModel` with matching types.
- No orphaned references to `FolderWatcherService` / `ScheduledSortService` / `RuleEngine` / `FileSearchService` remain in `MainViewModel`.

---

## Testing notes
**Not compiled here.** WPF binding failures are silent at runtime. After building, exercise specifically:

1. **Automation tab** — add a rule, add/remove a condition, browse a destination, add a watch folder, Start/Stop Watching, Start/Stop Scheduler, Run Sweep Now, Clear log. Confirm status-bar messages still appear (this proves `IStatusSink` is wired).
2. **Search tab** — browse a folder, run a search, cancel one, Open a result, clear results.
3. **Persistence** — add rules, Save Configuration, restart, confirm rules and schedule settings reload.
4. **History tab** — the Re-run button (it still binds to `MainViewModel`, so it's the best canary that the unchanged tabs weren't disturbed).
5. **Settings honoured** — automation should still use your verification/retry settings (via `BuildAutomationConfig`).

---

## Next steps
3. Extract `SessionContext` for `SourceFolder`/`DestinationFolder`, and broaden `IStatusSink` to cover the banner — `StatusMessage` still has heavy usage in the remaining code.
4. Extract the remaining tab ViewModels: Duplicates, Exceptions, History, Statistics, then Operations last (it owns queue, undo, and resume).

---

*End of Build 1.4.2 Changelog*
