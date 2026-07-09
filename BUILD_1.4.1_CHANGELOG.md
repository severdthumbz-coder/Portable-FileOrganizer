# FileOrganizer v5.0 - Build 1.4.1 Changelog

**Release Date:** July 9, 2026
**Build Type:** UI Architecture Refactor (Step 1 of God-object cleanup)

---

## Overview
Build 1.4.1 is a **pure refactor with no behavior change**. The nine tabs, which all lived inside a single 4,026-line `MainWindow.xaml`, now each live in their own `UserControl` under `Views/`.

This is Step 1 of an incremental plan to dismantle the God object. It was chosen first because it carries essentially zero logic risk: every view inherits the same `DataContext` (`MainViewModel`), so all bindings, commands, and features resolve exactly as before.

---

## What changed

### MainWindow.xaml: 4,026 → ~310 lines (‑92%)
It is now just a shell: header, theme toggle, the `TabControl` (headers only), status bar, and the notification banner. Each `TabItem` contains a single line:
```xml
<views:ConfigurationTabView/>
```

### New: `Views/` (9 UserControls)
| View | Lines extracted |
|---|---|
| `ConfigurationTabView` | 973 |
| `HelpTabView` | 1,561 |
| `DuplicatesTabView` | 278 |
| `StatisticsTabView` | 215 |
| `AutomationTabView` | 215 |
| `OperationsTabView` | 206 |
| `ExceptionsTabView` | 111 |
| `SearchTabView` | 103 |
| `HistoryTabView` | 57 |

### Converters moved to application scope
The six converters were declared in `Window.Resources`. A `UserControl` loaded separately cannot resolve `{StaticResource ...}` from its parent Window's resources, so **all converters moved to `App.xaml`** (`Application.Resources`).

Verified safe: `App.ApplyTheme()` clears only `MergedDictionaries`, not `Resources`, so theme switching does not affect the converters.

### Event handlers relocated
Handlers must live in the code-behind of the file containing their `x:Name`d elements:
- `RemoveSelectedSourceFolders_Click` + `SourceFoldersDataGrid` → `Views/ConfigurationTabView.xaml.cs`
- `RemoveSelectedExceptions_Click` + `ExceptionsDataGrid` → `Views/ExceptionsTabView.xaml.cs`
- `ThemeToggleButton_Click`, `ThemeIcon`, `CompletionBanner` → remain in `MainWindow` (they sit outside the TabControl)

---

## What did NOT change
- No ViewModel logic was touched. `MainViewModel` is byte-for-byte unchanged apart from the version string.
- No bindings were rewritten. Views inherit the Window's `DataContext`.
- No services, models, or engines were modified.
- No `csproj` item changes needed — the SDK-style project auto-includes `Views/**/*.xaml` and `**/*.cs`.

---

## Verification performed
- All 12 XAML files parse as well-formed.
- All C# files brace-balanced.
- Every view's `x:Class` matches its code-behind namespace + class.
- Every `Click=` handler exists in the *same file's* code-behind (no orphans).
- Every `x:Name` referenced from code-behind exists in that file's XAML.
- All six converters used in views are registered in `App.xaml`.
- No `RelativeSource AncestorType=Window` bindings (these would have silently broken).
- No cross-view `ElementName` bindings.
- `xmlns:models` declared in the two views that use it.

---

## Known limitations & testing notes
- **Not compiled here.** WPF binding errors fail *silently at runtime*, not at build. After building, click through **every tab** and confirm each renders and its controls work.
- Highest-risk spots to check first: **Configuration** (Remove Selected source folders), **Exceptions** (Remove Selected), the **theme toggle**, and any control using a converter (checkboxes bound via `EnumToBoolConverter`, the performance monitor's visibility, the Search tab's Cancel button).
- If a tab renders blank, the usual cause is an unresolved `StaticResource` — check `App.xaml`.

---

## Next steps in the refactor (not in this build)
2. Extract the genuinely independent ViewModels (`SearchViewModel`, `AutomationViewModel`).
3. Introduce shared services (`IStatusService`, `SessionContext`) to absorb the coupling — `StatusMessage` alone has 93 references.
4. Extract the remaining tab ViewModels, Operations last (it owns queue, undo, and resume).

---

*End of Build 1.4.1 Changelog*
