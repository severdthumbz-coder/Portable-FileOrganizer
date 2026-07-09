using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileOrganizer.Services
{
    /// <summary>A single search hit.</summary>
    public class SearchHit
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string Directory { get; set; }
        public long SizeBytes { get; set; }
        public DateTime Modified { get; set; }
        /// <summary>Where the match came from: "Name" or "Content".</summary>
        public string MatchedIn { get; set; }
        /// <summary>A short excerpt of surrounding text for content matches.</summary>
        public string Snippet { get; set; } = string.Empty;

        public string SizeFormatted
        {
            get
            {
                double b = SizeBytes;
                string[] units = { "B", "KB", "MB", "GB", "TB" };
                int i = 0;
                while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
                return $"{b:0.##} {units[i]}";
            }
        }

        public string ModifiedFormatted => Modified.ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>Progress information while a search runs.</summary>
    public class SearchProgress
    {
        public int FilesScanned { get; set; }
        public int Hits { get; set; }
        public string CurrentFile { get; set; }
    }

    /// <summary>
    /// Searches a folder tree by file name and (optionally) by file content, using
    /// ContentExtractor to read PDFs, Office documents, and text files.
    ///
    /// Content search is opt-in because it reads every candidate file. Name search is cheap.
    /// </summary>
    public class FileSearchService
    {
        /// <summary>Stop after this many hits so a broad query can't run away.</summary>
        public const int MaxResults = 500;

        /// <summary>
        /// Runs a search. Never throws; unreadable files are skipped.
        /// </summary>
        /// <param name="rootFolders">Folders to search.</param>
        /// <param name="query">Text to look for (case-insensitive).</param>
        /// <param name="searchContent">If true, also read file contents.</param>
        /// <param name="includeSubfolders">Recurse into subdirectories.</param>
        public async Task<List<SearchHit>> SearchAsync(
            IEnumerable<string> rootFolders,
            string query,
            bool searchContent,
            bool includeSubfolders,
            IProgress<SearchProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var hits = new List<SearchHit>();
            if (string.IsNullOrWhiteSpace(query)) return hits;

            int scanned = 0;
            var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var root in rootFolders ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

                string[] files;
                try
                {
                    files = Directory.GetFiles(root, "*", option);
                }
                catch
                {
                    continue; // unreadable folder — skip rather than abort the whole search
                }

                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested) return hits;
                    if (hits.Count >= MaxResults) return hits;

                    scanned++;
                    progress?.Report(new SearchProgress
                    {
                        FilesScanned = scanned,
                        Hits = hits.Count,
                        CurrentFile = Path.GetFileName(file)
                    });

                    FileInfo info;
                    try { info = new FileInfo(file); } catch { continue; }

                    // 1) Name match (cheap).
                    if (info.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hits.Add(BuildHit(info, "Name", string.Empty));
                        continue; // already a hit; don't pay for content extraction
                    }

                    // 2) Content match (expensive, opt-in, only for supported types).
                    if (!searchContent) continue;
                    if (!ContentExtractor.IsSupported(file)) continue;

                    var extraction = await ContentExtractor.ExtractAsync(file, cancellationToken);
                    if (!extraction.Success || string.IsNullOrEmpty(extraction.Text)) continue;

                    int idx = extraction.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        hits.Add(BuildHit(info, "Content", BuildSnippet(extraction.Text, idx, query.Length)));
                    }
                }
            }

            return hits;
        }

        private static SearchHit BuildHit(FileInfo info, string matchedIn, string snippet) => new SearchHit
        {
            FileName = info.Name,
            FullPath = info.FullName,
            Directory = info.DirectoryName,
            SizeBytes = info.Length,
            Modified = info.LastWriteTime,
            MatchedIn = matchedIn,
            Snippet = snippet
        };

        /// <summary>Returns ~60 characters of context around the match.</summary>
        private static string BuildSnippet(string text, int index, int queryLength)
        {
            const int pad = 60;
            int start = Math.Max(0, index - pad);
            int end = Math.Min(text.Length, index + queryLength + pad);
            var snippet = text.Substring(start, end - start).Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (start > 0) snippet = "…" + snippet;
            if (end < text.Length) snippet += "…";
            return snippet;
        }
    }
}
