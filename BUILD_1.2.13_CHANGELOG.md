# FileOrganizer v5.0 - Build 1.2.13 Changelog

**Release Date:** July 8, 2026
**Build Type:** External Copy Verification & Engine Fixes

---

## Overview
Build 1.2.13 makes the TeraCopy and FastCopy engines safe and verifiable. It adds an opt-out "Verify external copies" setting (default ON), fixes several real bugs in how the external tools were invoked, and guarantees that a move can never delete the source before the copy is verified.

---

## New: Verify External Copies (default ON)
A new setting in the Configuration tab. When enabled, copies performed by TeraCopy or FastCopy are verified before completion:

1. **Tool-native verification** — FastCopy is run with `/verify` (MD5/SHA-1); TeraCopy uses its built-in CRC check. This is efficient because the tool verifies inline during the copy.
2. **App-side sanity check** — after the tool finishes, the app confirms the destination file exists and its size matches the source.
3. **Independent hash (optional)** — if the global Verification Mode is set to **Full Hash**, the app also computes and compares SHA-256 of source and destination, independent of the tool.

When disabled, the engines behave as before (trust the tool's exit code), which is faster but unverified.

---

## Safe moves with external engines
Previously, TeraCopy/FastCopy in move mode deleted the source themselves — so a failed or corrupted copy could destroy the original before anything was checked.

Now the external tools are **always run in copy mode**, and the source is deleted by the app **only after** the copy (and any verification) succeeds. On a verification failure during a move, the source is left untouched and the file is reported as failed.

---

## Bug fixes

### FastCopy source argument
The source file was passed as `/srcfile="path"`. Per FastCopy's documentation, `/srcfile` specifies a **text file listing** source paths (one per line), not the file to copy. Sources must be positional arguments. Fixed to `fastcopy.exe /cmd=... "source" /to="destFolder\"`. Without this fix, FastCopy copies would not target the intended file.

### Renames lost with external engines
Both external tools copy into the destination **folder** using the original filename, so any operation that also renamed the file silently kept the old name. The app now renames the landed file into its intended final name after the tool completes.

### TeraCopy /Close + /NoClose conflict
The TeraCopy command line emitted both `/Close` and `/NoClose`, which are mutually exclusive — behavior was undefined. Removed `/NoClose`; the engine runs headless with `/Close` and `/Silent`.

---

## Files Modified
- `Models/Config.cs` — new `VerifyExternalCopies` setting (default true)
- `Services/MoveEngine.cs` — external copies run in copy mode and route through a new `FinalizeExternalCopyAsync` (rename + verify + safe source deletion); shared SHA-256 helper
- `Services/FastCopyEngine.cs` — positional source argument fix
- `Services/TeraCopyEngine.cs` — removed `/NoClose` conflict
- `ViewModels/MainViewModel.cs` — `VerifyExternalCopies` property; save/load wiring
- `MainWindow.xaml` — Verify External Copies checkbox; changelog entry; version strings
- `SplashScreen.xaml` — version string
- `FileOrganizer.csproj` — version fields (5.0.2.13)

---

## Notes & Limitations
- The independent SHA-256 comparison (Full Hash mode) reads the destination file fully; on very large files this adds time. Smart/SizeOnly modes rely on the tool's own verify plus the size check to stay fast.
- These changes could not be tested here against real TeraCopy/FastCopy installations; please validate a small copy and a small move (with Verify ON) before relying on it for large batches.

---

*End of Build 1.2.13 Changelog*
