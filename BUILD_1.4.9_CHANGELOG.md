# Build 1.4.9 — ConfigurationViewModel Extracted (Refactor Step 4e — God Object Fully Dismantled)

**Version:** `5.0.4.9`  ⇔  in-app **Build 1.4.9**
**Type:** Refactor (final MVVM extraction). No user-facing behavior change.

---

## Summary

This is the last extraction of the incremental "strangle" that began at Build 1.4.1. The
Configuration tab — the only tab still driven directly by `MainViewModel` — now has its own
`ConfigurationViewModel`. With this, **every one of the nine tabs has its own ViewModel**, and
`MainViewModel` is a pure coordinator.

`MainViewModel` drops from **1,121 → 512 lines** (from 3,364 at the start of the refactor).

---

## What moved to `ConfigurationViewModel`

- **All Configuration-tab settings** (~23 properties): scan mode, storage-type override
  (Auto/NVMe/SSD/HDD), copy engine, structure mode, conflict resolution, date organization,
  timestamp preservation, external-copy verification + mode, date format, continue-on-errors,
  retry attempts / delay — each with its description/side-effect logic intact.
- **The five enum-wrapper item lists** bound to the combo boxes (`ScanModes`, `CopyEngines`,
  `ConflictResolutions`, `VerificationModes`, `DateFormats`).
- **Engine detection** state and the `DetectEngine()` command (uses `Services.EngineDetector`).
- **Space analysis** state and the `AnalyzeSpace()` command.
- **The tab's commands**: Browse Source/Destination, Add/Remove Source Folder, Detect Engine,
  Analyze Space, Save, Clear.

## What stayed in `MainViewModel`

- **Cross-tab persistence orchestration**: `SaveConfig()` / `ClearConfig()` / `LoadPersistedData()`.
  These touch Configuration settings *plus* SessionContext, Automation, Exceptions, and the source
  folders, so they stay coordinated in one place. The Configuration-settings block is now supplied
  by `ConfigurationViewModel.BuildConfig(Config)` / `ApplyConfig(Config)`.
- `TestNotifications()` and its command — its button lives on the **Help** tab
  (DataContext = MainViewModel), not the Configuration tab.
- The scan-pipeline exception filter (`ApplyExceptionFilters`), unchanged.

---

## Design notes

- **Session state stays session-backed.** `SourceFolder`, `DestinationFolder`, `OperationMode`,
  `UseMultipleSources`, and `SourceFolders` live in `SessionContext`. `ConfigurationViewModel`
  forwards to it, so a History Re-run and the Configuration tab share one source of truth, and the
  storage-capability refresh side effect (in `SessionContext`) still fires for every writer.
- **The storage-detection description text** (`ScanModeDescription`, `SystemDetectedDescription`)
  is owned by `ConfigurationViewModel`, so the `SessionContext.SourceFolderChanged` re-raise moved
  there from `MainViewModel`.
- **Save/Clear buttons live on the Configuration tab** but the work is cross-tab, so
  `ConfigurationViewModel` receives a small `IConfigPersistence` interface (implemented by
  `MainViewModel`) and its `SaveConfigCommand` / `ClearConfigCommand` are thin forwarders — the
  same pattern used for `IOperationsSettingsProvider` and `IStatsSink` in earlier builds.
- **Settings-provider interfaces re-pointed.** `MainViewModel`'s `IOperationsSettingsProvider`
  getters and `ITransferSettingsProvider.BuildAutomationConfig()` now read `ConfigVM.*`. The
  interfaces stay **on** `MainViewModel`, so no downstream construction changed.
- **Construction order.** `ConfigVM` is constructed early (right after the notification service),
  because the settings-provider impls and the Duplicates scan-mode closure read `ConfigVM.*`.

## Minor fix included

- **Storage-override radios now raise change notifications when cleared.** Previously, selecting
  one storage-override option mutated the other three backing fields silently (no
  `PropertyChanged`). Selecting a new option now notifies the other three, so the bound UI can
  never display a stale selection. Behavior is otherwise identical.

---

## Wiring changes

- `MainWindow.xaml`: `<views:ConfigurationTabView DataContext="{Binding ConfigVM}"/>`.
- `Views/ConfigurationTabView.xaml.cs`: the `RemoveSelectedSourceFolders_Click` handler now casts
  its DataContext to `ConfigurationViewModel`.

## New files

- `ViewModels/ConfigurationViewModel.cs`
- `ViewModels/IConfigPersistence.cs`

---

## Validation performed (sandbox cannot compile WPF)

- XAML well-formedness on all touched views — pass.
- C# brace/paren balance on new and edited files — balanced.
- Structural depth walk (every member at class depth) — pass.
- Enum-member / service-API / Config-field cross-check for `ConfigurationViewModel` — all resolve
  (`ScanMode`, `CopyEngine`, `DestinationStructureMode`, `FileConflictResolution`,
  `VerificationMode`, `StorageType`; `AdaptivePerformanceManager`, `EngineDetector`; all 16 Config
  fields).
- **Binding-resolution sweep**: all 48 bindings in `ConfigurationTabView.xaml` resolve on
  `ConfigurationViewModel`.

**CI is the real compiler** — watch `gh run watch` after push.
