using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Scans directories and categorizes files
    /// </summary>
    public class FileScanner
    {
        private readonly Dictionary<string, string> _extensionCategories;

        public FileScanner()
        {
            // Initialize extension to category mappings
            _extensionCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Documents
                {".pdf", "Documents"}, {".doc", "Documents"}, {".docx", "Documents"},
                {".txt", "Documents"}, {".rtf", "Documents"}, {".odt", "Documents"},
                {".xls", "Documents"}, {".xlsx", "Documents"}, {".ppt", "Documents"},
                {".pptx", "Documents"}, {".csv", "Documents"},

                // Images
                {".jpg", "Images"}, {".jpeg", "Images"}, {".png", "Images"},
                {".gif", "Images"}, {".bmp", "Images"}, {".svg", "Images"},
                {".webp", "Images"}, {".tiff", "Images"}, {".ico", "Images"},

                // Videos
                {".mp4", "Videos"}, {".avi", "Videos"}, {".mkv", "Videos"},
                {".mov", "Videos"}, {".wmv", "Videos"}, {".flv", "Videos"},
                {".webm", "Videos"}, {".m4v", "Videos"},

                // Audio
                {".mp3", "Audio"}, {".wav", "Audio"}, {".flac", "Audio"},
                {".m4a", "Audio"}, {".aac", "Audio"}, {".ogg", "Audio"},
                {".wma", "Audio"},

                // Archives
                {".zip", "Archives"}, {".rar", "Archives"}, {".7z", "Archives"},
                {".tar", "Archives"}, {".gz", "Archives"}, {".bz2", "Archives"},

                // Code
                {".cs", "Code"}, {".java", "Code"}, {".py", "Code"},
                {".js", "Code"}, {".cpp", "Code"}, {".h", "Code"},
                {".html", "Code"}, {".css", "Code"}, {".xml", "Code"},
                {".json", "Code"}, {".sql", "Code"},

                // Executables
                {".exe", "Programs"}, {".msi", "Programs"}, {".bat", "Programs"},
                {".cmd", "Programs"}, {".ps1", "Programs"}
            };
        }

        /// <summary>
        /// Scans a directory and returns a list of files with their categories
        /// </summary>
        public async Task<List<QueueEntry>> ScanDirectoryAsync(
            string sourcePath, 
            ScanMode scanMode, 
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
            }

            try
            {
                var files = await Task.Run(() => 
                    Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories), 
                    cancellationToken);

                var totalFiles = files.Length;
                
                // Determine parallelism based on ScanMode
                int maxParallelism = GetParallelismLevel(scanMode, totalFiles);
                
                // Use parallel processing if maxParallelism > 1
                if (maxParallelism > 1)
                {
                    return await ScanParallelAsync(files, totalFiles, maxParallelism, progress, cancellationToken);
                }
                else
                {
                    // Single-threaded scan (for very small sets or if needed)
                    return await ScanSequentialAsync(files, totalFiles, progress, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error scanning directory: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Determines the parallelism level based on scan mode and file count
        /// Uses adaptive performance manager to optimize for system capabilities
        /// </summary>
        private int GetParallelismLevel(ScanMode mode, int fileCount)
        {
            var performanceManager = AdaptivePerformanceManager.Instance;
            return performanceManager.GetOptimalThreadCount(mode, fileCount, null);
        }

        /// <summary>
        /// Parallel file scanning using multiple threads
        /// </summary>
        private async Task<List<QueueEntry>> ScanParallelAsync(
            string[] files,
            int totalFiles,
            int maxParallelism,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var results = new System.Collections.Concurrent.ConcurrentBag<QueueEntry>();
            var processedFiles = 0;
            var progressLock = new object();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelism,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(files, parallelOptions, async (file, token) =>
            {
                if (token.IsCancellationRequested)
                    return;

                await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(file);
                    var category = GetCategory(fileInfo.Extension);

                    results.Add(new QueueEntry
                    {
                        FileName = fileInfo.Name,
                        SourcePath = file,
                        Category = category,
                        SizeBytes = fileInfo.Length,
                        Status = "Pending"
                    });

                    // Thread-safe progress reporting
                    lock (progressLock)
                    {
                        processedFiles++;
                        if (processedFiles % 100 == 0 || processedFiles == totalFiles)
                        {
                            progress?.Report((double)processedFiles / totalFiles * 100);
                        }
                    }
                }, token);
            });

            return results.ToList();
        }

        /// <summary>
        /// Sequential (single-threaded) file scanning
        /// </summary>
        private async Task<List<QueueEntry>> ScanSequentialAsync(
            string[] files,
            int totalFiles,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var results = new List<QueueEntry>();
            var processedFiles = 0;

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(file);
                    var category = GetCategory(fileInfo.Extension);

                    results.Add(new QueueEntry
                    {
                        FileName = fileInfo.Name,
                        SourcePath = file,
                        Category = category,
                        SizeBytes = fileInfo.Length,
                        Status = "Pending"
                    });

                    processedFiles++;
                    progress?.Report((double)processedFiles / totalFiles * 100);
                }, cancellationToken);
            }

            return results;
        }

        /// <summary>
        /// Quick scan - only top level directory
        /// </summary>
        public List<QueueEntry> QuickScan(string sourcePath)
        {
            var results = new List<QueueEntry>();

            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
            }

            var files = Directory.GetFiles(sourcePath, "*.*", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var category = GetCategory(fileInfo.Extension);

                results.Add(new QueueEntry
                {
                    FileName = fileInfo.Name,
                    SourcePath = file,
                    Category = category,
                    SizeBytes = fileInfo.Length,
                    Status = "Pending"
                });
            }

            return results;
        }

        /// <summary>
        /// Gets the category for a file extension
        /// </summary>
        public string GetCategory(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return "Other";

            return _extensionCategories.TryGetValue(extension, out var category) 
                ? category 
                : "Other";
        }

        /// <summary>
        /// Estimates scan time based on file count and scan mode
        /// </summary>
        public TimeSpan EstimateScanTime(int fileCount, ScanMode mode)
        {
            // Very rough estimates
            double filesPerSecond = mode switch
            {
                ScanMode.Turbo => 10000,
                ScanMode.Fast => 5000,
                ScanMode.Normal => 2000,
                ScanMode.Auto => 3000,
                _ => 2000
            };

            var seconds = fileCount / filesPerSecond;
            return TimeSpan.FromSeconds(Math.Max(1, seconds));
        }
    }
}
