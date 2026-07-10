# FileOrganizer v5.0 - Build 1.4.3 Changelog

**Release Date:** July 9, 2026
**Build Type:** Architecture Refactor — Step 3 (Shared Services)

---

## Overview
Step 3 attacks the coupling that was holding the remaining tabs inside the God object. Measured before starting:

| Shared member | References in MainViewModel |
|---|---|
| `StatusMessage` | 73 |
| `SourceFolder` | 41 |
| `DestinationFolder` | 28 |
| `ShowCompletionBanner` | 19 |
| `OperationMode` | 15 |

Those references are why the other five tabs couldn't be extracted. This build gives that state a proper home.

**No behavior change.**

---

## What changed

### New: `INotificationService` + `NotificationService`
The status line and the completion banner are the same concern — "tell the user something" — so they now sit behind one service. `NotificationService` owns the `BannerNotification` control reference (previously a private field on `MainViewModel`) and writes the status bar through a callback.

This replaces the narrower `IStatusSink` introduced in 1.4.2, which is now deleted.

### New: `SessionContext`
Holds the shared "what are we operating on" state: `SourceFolder`, `DestinationFolder`, `OperationMode`, `UseMultipleSources`, and the `SourceFolders` collection. It derives from `ViewModelBase`, so it is fully observable and views can bind to it directly as tabs are extracted.

### Sharper interface boundaries
`ITransferSettingsProvider` was doing two unrelated jobs. It now covers **only** how files are transferred (`BuildAutomationConfig()`); *where* files come from and go to is `SessionContext`'s responsibility.

As a result:
- `SearchViewModel` now takes `SessionContext` (it only ever needed `DestinationFolder`).
- `AutomationViewModel` takes `INotificationService`, `ITransferSettingsProvider`, and `SessionContext`.

### 92 call sites untouched
`StatusMessage` remains a bindable property; `NotificationService` writes it. `ShowCompletionBanner` remains a private helper that delegates to the service. `SourceFolder` and friends remain properties on `MainViewModel` that now delegate to `_session`.

That means **all 73 status calls, 19 banner calls, and every XAML binding keep working with zero edits** — while the state itself has exactly one home.

---

## Subtle issues caught during the refactor
- **Default operation mode.** `MainViewModel._operationMode` defaulted to `Move`. My first draft of `SessionContext` defaulted to `Copy`, which would have silently flipped the app's default operation. Corrected to `Move`.
- **Setter side effects preserved.** `SourceFolder`'s setter refreshes `AdaptivePerformanceManager` capabilities and raises `SystemDetectedDescription` / `ScanModeDescription`; `OperationMode`'s setter raises `ShowLiveMoveButton` / `ShowLiveCopyButton`. Both preserved exactly in the delegating wrappers.
- **Initialization order.** `_session` is a field initializer (runs before the constructor body); `_notifications` is created before the child ViewModels that receive it.
- **Automation's `DestinationFolder` bindings** are rule-scoped (`SelectedRule.DestinationFolder` and a per-row DataGrid column), not ViewModel-scoped — so re-pointing that tab's DataContext in 1.4.2 did not break them.

---

## Files Added
- `ViewModels/INotificationService.cs`
- `ViewModels/NotificationService.cs`
- `ViewModels/SessionContext.cs`

## Files Removed
- `ViewModels/IStatusSink.cs` (superseded by `INotificationService`)

## Files Modified
- `ViewModels/MainViewModel.cs` — implements `INotificationService`; session properties delegate to `SessionContext`; banner logic moved out; dead backing fields removed
- `ViewModels/AutomationViewModel.cs` — takes `INotificationService` + `SessionContext`
- `ViewModels/SearchViewModel.cs` — takes `SessionContext`
- `ViewModels/ITransferSettingsProvider.cs` — narrowed to `BuildAutomationConfig()`
- `Views/HelpTabView.xaml`, `MainWindow.xaml`, `SplashScreen.xaml`, `FileOrganizer.csproj` — changelog + version

---

## Testing notes
**Not compiled here.** Binding failures are silent in WPF. After building, verify:

1. **Status bar** — perform any action (scan, save config, add a rule). Messages must still appear. This exercises `NotificationService`.
2. **Completion banner** — run a scan or a copy. The banner must still appear and auto-dismiss.
3. **Configuration tab** — browse a source folder. Confirm the *storage-type detection* text updates (this proves `SourceFolder`'s side effect survived the delegation).
4. **Move/Copy radio buttons** — switching must still show/hide the correct Live button (`OperationMode` side effect).
5. **Default mode** — on a fresh config, the app should default to **Move**, not Copy.
6. **Multiple sources** — toggle it, add/remove source folders, confirm the grid updates.
7. **Automation + Search tabs** — still fully functional (they now read folders from `SessionContext`).

---

## Next steps
4. Extract the remaining tab ViewModels now that the shared state has a home: Duplicates, Exceptions, History, Statistics — then Operations last, since it owns the queue, undo, and resume.

---

*End of Build 1.4.3 Changelog*
