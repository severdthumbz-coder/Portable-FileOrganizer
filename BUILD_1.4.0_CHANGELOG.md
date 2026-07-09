# FileOrganizer v5.0 - Build 1.4.0 Changelog

**Release Date:** July 8, 2026
**Build Type:** Major Feature — Tier 3: Content-Aware Sorting & Full-Text Search

---

## Overview
Build 1.4.0 lets FileOrganizer look *inside* files. Rules can now match on document text rather than just filenames, and a new Search tab finds files by name or by their contents. This closes the last gap against content-aware competitors (File Juggler, Hazel, and the newer AI organizers).

---

## New: Content-Aware Rules
Two new rule conditions:
- **Content contains** — case-insensitive substring match against the file's text.
- **Content matches regex** — regular-expression match against the file's text.

A lease scanned as `scan_003.pdf` can now be routed by the word "lease" inside it.

### Supported formats
| Format | How it's read | Extra dependency |
|---|---|---|
| `.docx`, `.xlsx`, `.pptx` | OOXML (zip + XML) via `System.IO.Compression` | **None** |
| `.txt`, `.md`, `.csv`, `.json`, `.xml`, `.html`, code files… | Direct text read | **None** |
| `.pdf` | PdfPig (Apache 2.0) | **PdfPig** (optional) |

### Efficiency
Content extraction is the expensive part, so it's minimized:
- **Lazy** — text is only extracted if a content condition is actually reached.
- **Cached** — each file is read at most once per evaluation, even with several content conditions.
- **Cheap-first** — metadata conditions (extension, name, size, date) are evaluated before any content condition, so a failing `Extension is .pdf` short-circuits before the disk is touched.

### Safety limits
- Files over **50 MB** are skipped for content matching.
- Extracted text is truncated at **200,000 characters**.
- Unreadable/unsupported files simply don't match; they never throw.

---

## New: Search tab
Search a folder tree by file name, and optionally inside file contents.
- Toggle **Search inside file contents** and **Include subfolders**.
- Results show the file, **what matched** (Name or Content), size, modified date, a **snippet** of surrounding text, and the folder.
- **Open** button reveals the file in Explorer.
- Searches are cancellable and capped at 500 results.

---

## PDF support (optional dependency)
PDF text extraction uses **PdfPig**, chosen because it is Apache-2.0 licensed and free for commercial use, unlike iText 7 which is AGPL and needs a commercial license for closed-source products.

`ContentExtractor` loads PdfPig **reflectively**, so:
- If the package is present → PDFs are searched and matched.
- If it's absent → the app still compiles and runs; PDFs report *"PDF support not installed"* and simply don't match.

A `<PackageReference Include="PdfPig" Version="0.1.9" />` has been added. Restore it with:
```
dotnet restore
```
Note: PdfPig is pre-1.0, and its maintainers state that minor versions may change the public API without warning until 1.0.0. The reflective loading insulates the app from that.

Per PdfPig's own guidance, the extractor prefers `ContentOrderTextExtractor.GetText(page)` over `page.Text` (which preserves internal content order rather than reading order), falling back to `page.Text` only if the extractor type isn't available.

---

## Breaking change (internal)
`RuleEngine.Evaluate(...)` is now **`RuleEngine.EvaluateAsync(...)`**, because content extraction is I/O-bound. Both callers (`FolderWatcherService`, `ScheduledSortService`) were updated. Any external code calling `Evaluate` must be updated.

---

## Files Added
- `Services/ContentExtractor.cs` — text extraction for Office/text/PDF
- `Services/FileSearchService.cs` — name + content search

## Files Modified
- `Models/OrganizationRule.cs` — `ContentContains`, `ContentMatchesRegex` conditions
- `Services/RuleEngine.cs` — async, content-aware, lazy+cached extraction, cheap-conditions-first
- `Services/FolderWatcherService.cs`, `Services/ScheduledSortService.cs` — await `EvaluateAsync`
- `ViewModels/MainViewModel.cs` — search state, commands, and implementation
- `MainWindow.xaml` — new Search tab, Recommended Workflow steps 9–10, Features, changelog, version
- `FileOrganizer.csproj` — PdfPig package reference; version 5.0.4.0
- `SplashScreen.xaml` — version

---

## Known limitations & testing notes
- **Not compiled/tested here** (my sandbox can't reach nuget.org). Needs a local `dotnet restore` + build.
- **Scanned/image PDFs** contain no text layer; they will not match content rules. OCR is out of scope.
- **XLSX** extraction reads the shared-strings table, which covers most cell text but not values stored inline or as formulas.
- **Content search is slow on large trees** — it opens every supported file. Start with a narrow folder, or leave "search inside contents" off for a quick name-only pass.
- Content rules are best paired with a cheap condition (e.g. `Extension is .pdf`) so only relevant files are opened.

---

*End of Build 1.4.0 Changelog*
