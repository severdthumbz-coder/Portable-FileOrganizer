# Build 1.5.0 — Guided Onboarding & System Tray

**Version:** `5.0.5.0`  ⇔  in-app **Build 1.5.0**
**Type:** Feature build (first feature build after the MVVM refactor completed at 1.4.9).

---

## Summary

Two user-facing capabilities, plus the plumbing to support them:

1. **Guided onboarding** — a first-run guided tour, ⓘ hint bubbles on every tab, and a ❔ legend,
   all from the reusable **OnboardingKit** (shared with Video Metadata Editor).
2. **System tray** — minimize-to-tray and close-to-tray (each an opt-in setting), a single-instance
   guard, and an automation-aware tray menu so folder watching / scheduled sweeps can run headless.

No existing behaviour changed; everything here is additive.

---

## 1. Onboarding (OnboardingKit integration)

- **Guided tour** — 9 steps in workflow order (Welcome → Configuration → Operations → Exceptions →
  Automation → Duplicates → Search/History → Help → "You're all set"). Auto-starts on first launch
  after the window has drawn; replay any time from the Help tab. Keyboard: → / Enter next, ← back,
  Esc skip.
- **ⓘ hints** — one `HintMarker` beside every tab header (all 9), each a 1–2 sentence explanation.
- **❔ Legend** — a header button (top-right) opening a flyout that explains the app's status glyphs
  (✓ / ⚠ / ❌) and the accent colour; the same list is embedded on the Help tab.
- **Master switch** — "Show onboarding helpers" on the Help tab hides the hints and ❔ button and
  stops the first-run tour. Replay always works regardless.

### Integration details
- The kit's `Onboarding/` folder was dropped in unchanged (MIT, no new NuGet dependency).
- `App.xaml` merges `Onboarding/Onboarding.xaml` **before** the theme so the theme's `Onb*` aliases
  win.
- **Theme aliases** added inside both `LightTheme.xaml` and `DarkTheme.xaml` map the kit's `Onb*`
  brushes to the app's own brushes, so the tour/legend match the app and follow light/dark switching
  live.
- **`App.ApplyTheme()` was made non-destructive**: it previously did `MergedDictionaries.Clear()`,
  which would have dropped `Onboarding.xaml` on every theme toggle. It now replaces **only** the
  theme dictionary (identified by its `Themes/` source path), preserving all others.
- Tour steps and legend rows live in the one app-specific file, `OnboardingContent.cs`.
- Flags (`ShowOnboardingHelpers`, `HasSeenTour`) are stored in the existing `config.json` via a
  lightweight read-modify-write path on `ConfigManager` (see §3), so toggling helpers persists
  immediately without routing through the main Save/Load orchestration.
- `<Nullable>` changed from `disable` to `annotations` so the kit's nullable reference syntax
  compiles without CS8632, while keeping null-state warnings off for the rest of the app.

## 2. System tray

- **`TrayIconManager`** (new, `Tray/TrayIconManager.cs`) owns a `System.Windows.Forms.NotifyIcon`
  (WinForms is already referenced — no new package). The icon is pulled from the running EXE.
- **Minimize-to-tray** and **close-to-tray** are independent opt-in settings (Help tab → Window &
  Tray). Minimize hides the window when minimized; close hides instead of exiting and keeps
  automation running. **Exit** on the tray menu always truly quits.
- **Automation-aware context menu**, rebuilt on open to reflect live state: Open · Run Sweep Now ·
  Start/Stop Watching · Start/Pause Scheduler · Exit. Actions are driven through the existing
  `AutomationViewModel` commands (`RunSweepNowCommand`, `StartWatchingCommand`/`StopWatchingCommand`,
  `StartScheduleCommand`/`StopScheduleCommand`), gated by `CanExecute`.
- Double-click the tray icon (or "Open") restores the window.
- **Single instance** — a named `Mutex` detects an already-running instance; a named
  `EventWaitHandle` lets a second launch signal the first to re-show its window, then exits. Handles
  are released on exit. The second instance never owns the mutex, so it disposes without releasing.
- `MainWindow` owns the manager and drives its `StateChanged` / `Closing` hooks; the window stays in
  control of its own lifecycle.

## 3. Supporting changes

- `Models/Config.cs`: added `ShowOnboardingHelpers`, `HasSeenTour`, `MinimizeToTray`, `CloseToTray`.
- `Services/ConfigManager.cs`: added isolated `Load/SaveOnboardingFlags` and `Load/SaveTrayFlags`
  (read-modify-write of only those fields, so a toggle can't clobber unsaved Configuration-tab
  edits), plus a shared `LoadOrDefault()` helper.
- `ViewModels/MainViewModel.cs`: `MinimizeToTray` / `CloseToTray` bindable properties (persist on
  change); loaded from config in `LoadPersistedData`; `VersionInfo` → 1.5.0.
- `MainWindow.xaml`: `ob:` namespace; named the `TabControl` (`MainTabs`); `❔ LegendButton` in the
  header (white glyph for the coloured header); `HintMarker` on all 9 tab headers; `TourOverlay` as
  the last child of the root grid.
- `Views/HelpTabView.xaml`: "Guided Tour & Legend" card (replay button, helpers checkbox,
  `LegendList`) and "Window & Tray" card (the two tray checkboxes).

## 4. Known-trap notes for the next session

- The `❔` glyph is supplied as an explicit white `TextBlock` because `LegendButton`'s inner toggle
  sets its own `Foreground`, which breaks `Foreground` inheritance from the UserControl.
- `ApplyTheme` must keep preserving non-theme dictionaries — don't restore the old `Clear()`.

---

## Validation performed (sandbox cannot compile WPF)

- XAML well-formedness on all touched views and the kit's 4 XAML files — pass.
- C# brace/paren balance on all new/edited files — balanced.
- Structural depth walk on new C# — every member at class depth.
- API cross-check — `AutomationViewModel` commands/state, `ConfigManager` new methods,
  `OnboardingController` members, `DelegateOnboardingStore` ctor, and all WinForms/Drawing types all
  resolve.
- `ob:` type and binding sweep — `LegendButton`/`TourOverlay`/`LegendList`/`HintMarker`/
  `OnboardingController` exist; new Help-tab bindings resolve on `MainViewModel` /
  `OnboardingController.Current`.

**CI is the real compiler** — watch `gh run watch` after push. First real compile also validates the
OnboardingKit itself (its author couldn't compile it either).
