using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FileOrganizer.Services
{
    /// <summary>
    /// The outcome of attempting to read a file's text content.
    /// </summary>
    public class ExtractionResult
    {
        public bool Success { get; set; }
        public string Text { get; set; } = string.Empty;
        /// <summary>Why extraction didn't happen (unsupported type, too large, PDF support absent, error).</summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Extracts plain text from files so rules can match on content and files can be searched.
    ///
    /// Design notes:
    ///  - Office formats (.docx/.xlsx/.pptx) are OOXML: ZIP archives of XML. We read them with
    ///    System.IO.Compression, so NO additional NuGet package is required.
    ///  - Plain-text-ish formats are read directly.
    ///  - PDF requires a third-party library (PdfPig, Apache-2.0). It is loaded reflectively so the
    ///    application still compiles and runs if the package isn't installed; PDFs then report
    ///    "PDF support not installed" rather than crashing.
    ///  - Extraction is capped by size and character count so a huge file can't stall the UI.
    /// </summary>
    public static class ContentExtractor
    {
        /// <summary>Files larger than this are skipped (content matching is for documents, not media).</summary>
        public const long MaxFileSizeBytes = 50L * 1024 * 1024; // 50 MB

        /// <summary>Extracted text is truncated to this many characters.</summary>
        public const int MaxCharacters = 200_000;

        private static readonly HashSet<string> PlainTextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".csv", ".tsv", ".log", ".json", ".xml", ".html", ".htm",
            ".cs", ".js", ".ts", ".py", ".java", ".c", ".cpp", ".h", ".sql", ".yml", ".yaml", ".ini", ".config"
        };

        private static readonly HashSet<string> OfficeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".xlsx", ".pptx"
        };

        /// <summary>True if this file type can (in principle) have its text read.</summary>
        public static bool IsSupported(string path)
        {
            var ext = Path.GetExtension(path);
            return PlainTextExtensions.Contains(ext)
                || OfficeExtensions.Contains(ext)
                || ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the text of a file. Never throws; failures are reported via ExtractionResult.
        /// </summary>
        public static async Task<ExtractionResult> ExtractAsync(string path, CancellationToken cancellationToken = default)
        {
            var result = new ExtractionResult();

            try
            {
                if (!File.Exists(path))
                {
                    result.Reason = "File not found.";
                    return result;
                }

                var info = new FileInfo(path);
                if (info.Length > MaxFileSizeBytes)
                {
                    result.Reason = $"File too large for content extraction (> {MaxFileSizeBytes / (1024 * 1024)} MB).";
                    return result;
                }

                var ext = info.Extension;

                string text;
                if (PlainTextExtensions.Contains(ext))
                {
                    text = await ReadPlainTextAsync(path, cancellationToken);
                }
                else if (ext.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    text = ExtractOoxml(path, entryFilter: e =>
                        e.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase));
                }
                else if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    // Most cell text lives in the shared strings table.
                    text = ExtractOoxml(path, entryFilter: e =>
                        e.FullName.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));
                }
                else if (ext.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
                {
                    // Slide text is spread across slide XML parts.
                    text = ExtractOoxml(path, entryFilter: e =>
                        e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
                }
                else if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var pdf = TryExtractPdf(path, out var pdfReason);
                    if (pdf == null)
                    {
                        result.Reason = pdfReason;
                        return result;
                    }
                    text = pdf;
                }
                else
                {
                    result.Reason = "Unsupported file type for content extraction.";
                    return result;
                }

                if (text == null)
                {
                    result.Reason = "No readable text found.";
                    return result;
                }

                if (text.Length > MaxCharacters)
                    text = text.Substring(0, MaxCharacters);

                result.Success = true;
                result.Text = text;
                return result;
            }
            catch (Exception ex)
            {
                result.Reason = $"Extraction error: {ex.Message}";
                return result;
            }
        }

        private static async Task<string> ReadPlainTextAsync(string path, CancellationToken ct)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                var buffer = new char[Math.Min(MaxCharacters, 64 * 1024)];
                var sb = new StringBuilder();
                int read;
                while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    sb.Append(buffer, 0, read);
                    if (sb.Length >= MaxCharacters) break;
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Reads matching XML parts out of an OOXML (docx/xlsx/pptx) zip and strips tags,
        /// leaving the visible text. No external dependency required.
        /// </summary>
        private static string ExtractOoxml(string path, Func<ZipArchiveEntry, bool> entryFilter)
        {
            var sb = new StringBuilder();
            using (var zip = ZipFile.OpenRead(path))
            {
                foreach (var entry in zip.Entries.Where(entryFilter))
                {
                    using (var stream = entry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        var xml = reader.ReadToEnd();
                        sb.Append(StripXmlTags(xml));
                        sb.Append('\n');
                    }
                    if (sb.Length >= MaxCharacters) break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Converts OOXML markup to readable text: paragraph/break/row tags become whitespace,
        /// all other tags are removed, then XML entities are decoded.
        /// </summary>
        private static string StripXmlTags(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return string.Empty;

            // Turn structural boundaries into whitespace so words don't run together.
            xml = Regex.Replace(xml, @"<(w:p|w:br|w:tab|a:p|a:br|row|/row)\b[^>]*>", " ", RegexOptions.IgnoreCase);
            // Remove every remaining tag.
            xml = Regex.Replace(xml, @"<[^>]+>", string.Empty);
            // Decode common entities.
            xml = xml.Replace("&amp;", "&")
                     .Replace("&lt;", "<")
                     .Replace("&gt;", ">")
                     .Replace("&quot;", "\"")
                     .Replace("&apos;", "'");
            // Collapse runs of whitespace.
            xml = Regex.Replace(xml, @"\s+", " ");
            return xml.Trim();
        }

        // ---- PDF support (optional dependency: PdfPig, Apache 2.0) ----
        //
        // Loaded via reflection so the project compiles and runs whether or not the
        // package is present. If absent, PDFs simply report that support isn't installed.

        private static bool _pdfProbeDone;
        private static Type _pdfDocumentType;

        private static string TryExtractPdf(string path, out string reason)
        {
            reason = string.Empty;

            if (!_pdfProbeDone)
            {
                _pdfProbeDone = true;
                _pdfDocumentType = Type.GetType("UglyToad.PdfPig.PdfDocument, UglyToad.PdfPig", throwOnError: false);
            }

            if (_pdfDocumentType == null)
            {
                reason = "PDF support not installed (add the PdfPig NuGet package to enable PDF content matching).";
                return null;
            }

            try
            {
                // PdfPig: using (var doc = PdfDocument.Open(path)) foreach (var page in doc.GetPages()) ...
                var openMethod = _pdfDocumentType.GetMethod("Open", new[] { typeof(string) });
                if (openMethod == null)
                {
                    reason = "PDF library present but its API was not recognized.";
                    return null;
                }

                // PdfPig's docs advise against page.Text (it preserves internal content order,
                // which is rarely reading order). Prefer ContentOrderTextExtractor.GetText(page)
                // and fall back to page.Text only if that type isn't available.
                var extractorType = Type.GetType(
                    "UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor, UglyToad.PdfPig",
                    throwOnError: false);
                System.Reflection.MethodInfo getTextMethod = null;
                if (extractorType != null)
                {
                    getTextMethod = extractorType.GetMethods()
                        .FirstOrDefault(m => m.Name == "GetText" && m.GetParameters().Length == 1);
                }

                using (var doc = (IDisposable)openMethod.Invoke(null, new object[] { path }))
                {
                    var getPages = _pdfDocumentType.GetMethod("GetPages", Type.EmptyTypes);
                    var pages = getPages?.Invoke(doc, null) as System.Collections.IEnumerable;
                    if (pages == null)
                    {
                        reason = "PDF library present but no pages could be read.";
                        return null;
                    }

                    var sb = new StringBuilder();
                    foreach (var page in pages)
                    {
                        string pageText = null;

                        if (getTextMethod != null)
                        {
                            try { pageText = getTextMethod.Invoke(null, new[] { page }) as string; }
                            catch { pageText = null; }
                        }

                        if (string.IsNullOrEmpty(pageText))
                        {
                            var textProp = page.GetType().GetProperty("Text");
                            pageText = textProp?.GetValue(page) as string;
                        }

                        if (!string.IsNullOrEmpty(pageText))
                        {
                            sb.Append(pageText);
                            sb.Append('\n');
                        }
                        if (sb.Length >= MaxCharacters) break;
                    }
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                reason = $"PDF extraction failed: {ex.Message}";
                return null;
            }
        }
    }
}
