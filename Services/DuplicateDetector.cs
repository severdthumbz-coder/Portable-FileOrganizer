using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Detects duplicate files based on content hashing
    /// OPTIMIZED: Uses adaptive threading based on system capabilities
    /// </summary>
    public class DuplicateDetector
    {
        /// <summary>
        /// Finds duplicate files in a directory using multi-threaded hashing
        /// </summary>
        public async Task<DuplicateDetectionResult> DetectDuplicatesAsync(
            string sourcePath,
            ScanMode scanMode = ScanMode.Auto,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new DuplicateDetectionResult();

            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
            }

            try
            {
                // Get all files recursively
                var files = await Task.Run(() =>
                    Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories),
                    cancellationToken);

                result.TotalFilesScanned = files.Length;

                // Thread-safe dictionary for hash -> file paths
                var hashToFiles = new ConcurrentDictionary<string, ConcurrentBag<string>>();
                
                // Thread-safe counter for progress
                int processedFiles = 0;

                // Use adaptive performance manager to get optimal thread count
                var performanceManager = AdaptivePerformanceManager.Instance;
                int optimalThreads = performanceManager.GetOptimalThreadCount(scanMode, files.Length, sourcePath);

                // Process files in parallel with adaptive threading
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = optimalThreads,
                    CancellationToken = cancellationToken
                };

                await Task.Run(() =>
                {
                    Parallel.ForEach(files, parallelOptions, file =>
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);

                            // Skip empty files
                            if (fileInfo.Length == 0)
                            {
                                Interlocked.Increment(ref processedFiles);
                                return;
                            }

                            // Compute hash (this is the CPU-intensive part)
                            var hash = ComputeFileHashSync(file);

                            // Add to thread-safe dictionary
                            hashToFiles.AddOrUpdate(
                                hash,
                                new ConcurrentBag<string> { file },
                                (key, existing) =>
                                {
                                    existing.Add(file);
                                    return existing;
                                });
                        }
                        catch (Exception ex)
                        {
                            // Skip files that can't be accessed
                            System.Diagnostics.Debug.WriteLine($"Error hashing file {file}: {ex.Message}");
                        }

                        // Update progress (thread-safe)
                        var current = Interlocked.Increment(ref processedFiles);
                        if (current % 100 == 0 || current == files.Length)
                        {
                            progress?.Report((double)current / files.Length * 100);
                        }
                    });
                }, cancellationToken);

                // Find duplicates (groups with more than 1 file)
                foreach (var kvp in hashToFiles.Where(x => x.Value.Count > 1))
                {
                    var fileList = kvp.Value.ToList();
                    var group = new DuplicateGroup
                    {
                        Hash = kvp.Key,
                        Files = fileList,
                        FileCount = fileList.Count
                    };

                    // Calculate total size of duplicates (keeping one, wasting others)
                    var firstFile = new FileInfo(fileList[0]);
                    group.FileSize = firstFile.Length;
                    group.WastedSpace = firstFile.Length * (fileList.Count - 1);
                    
                    // Populate DuplicateFiles collection for UI
                    foreach (var filePath in fileList)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            group.DuplicateFiles.Add(new DuplicateFile
                            {
                                FilePath = filePath,
                                CreatedDate = fileInfo.CreationTime,
                                ModifiedDate = fileInfo.LastWriteTime,
                                FileSize = fileInfo.Length,
                                IsSelected = false,
                                IsRecommendedKeep = false
                            });
                        }
                        catch
                        {
                            // Skip files that can't be accessed
                        }
                    }

                    result.DuplicateGroups.Add(group);
                    result.TotalDuplicateFiles += fileList.Count - 1; // -1 because we keep one
                    result.TotalWastedSpace += group.WastedSpace;
                }

                result.DuplicateGroupCount = result.DuplicateGroups.Count;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error detecting duplicates: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// Synchronous hash computation for use in Parallel.ForEach
        /// </summary>
        private string ComputeFileHashSync(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        /// <summary>
        /// Async hash computation (kept for compatibility, but not used in Turbo mode)
        /// </summary>
        private async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hashBytes = await Task.Run(() => sha256.ComputeHash(stream), cancellationToken);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        /// <summary>
        /// Quick duplicate detection based on file size only (faster but less accurate)
        /// Also uses adaptive threading for maximum speed
        /// </summary>
        public async Task<DuplicateDetectionResult> QuickDetectDuplicatesAsync(
            string sourcePath,
            ScanMode scanMode = ScanMode.Auto,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new DuplicateDetectionResult();

            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
            }

            var files = await Task.Run(() =>
                Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories),
                cancellationToken);

            result.TotalFilesScanned = files.Length;

            // Thread-safe dictionary for size -> file paths
            var sizeGroups = new ConcurrentDictionary<long, ConcurrentBag<string>>();
            int processedFiles = 0;

            // Use adaptive performance manager to get optimal thread count
            var performanceManager = AdaptivePerformanceManager.Instance;
            int optimalThreads = performanceManager.GetOptimalThreadCount(scanMode, files.Length, sourcePath);

            // File size grouping benefits from parallel processing
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = optimalThreads,
                CancellationToken = cancellationToken
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(files, parallelOptions, file =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var size = fileInfo.Length;

                        sizeGroups.AddOrUpdate(
                            size,
                            new ConcurrentBag<string> { file },
                            (key, existing) =>
                            {
                                existing.Add(file);
                                return existing;
                            });
                    }
                    catch { }

                    var current = Interlocked.Increment(ref processedFiles);
                    if (current % 100 == 0 || current == files.Length)
                    {
                        progress?.Report((double)current / files.Length * 100);
                    }
                });
            }, cancellationToken);

            // Find potential duplicates (same size)
            foreach (var kvp in sizeGroups.Where(x => x.Value.Count > 1 && x.Key > 0))
            {
                var fileList = kvp.Value.ToList();
                var group = new DuplicateGroup
                {
                    Hash = $"Size-{kvp.Key}",
                    Files = fileList,
                    FileCount = fileList.Count,
                    FileSize = kvp.Key,
                    WastedSpace = kvp.Key * (fileList.Count - 1)
                };

                result.DuplicateGroups.Add(group);
                result.TotalDuplicateFiles += fileList.Count - 1;
                result.TotalWastedSpace += group.WastedSpace;
            }

            result.DuplicateGroupCount = result.DuplicateGroups.Count;
            return result;
        }
    }

    /// <summary>
    /// Result of duplicate detection
    /// </summary>
    public class DuplicateDetectionResult
    {
        public int TotalFilesScanned { get; set; }
        public int DuplicateGroupCount { get; set; }
        public int TotalDuplicateFiles { get; set; }
        public long TotalWastedSpace { get; set; }
        public List<DuplicateGroup> DuplicateGroups { get; set; } = new List<DuplicateGroup>();

        public double WastedSpaceGB => TotalWastedSpace / (1024.0 * 1024.0 * 1024.0);
    }

    /// <summary>
    /// Represents a group of duplicate files
    /// </summary>
    public class DuplicateGroup
    {
        public string Hash { get; set; }
        public List<string> Files { get; set; }
        public int FileCount { get; set; }
        public long FileSize { get; set; }
        public long WastedSpace { get; set; }
        
        // UI support
        public System.Collections.ObjectModel.ObservableCollection<DuplicateFile> DuplicateFiles { get; set; } 
            = new System.Collections.ObjectModel.ObservableCollection<DuplicateFile>();
        public bool IsExpanded { get; set; } = true;

        public string FileSizeFormatted
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F2} KB";
                if (FileSize < 1024 * 1024 * 1024) return $"{FileSize / (1024.0 * 1024.0):F2} MB";
                return $"{FileSize / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }

        public string WastedSpaceFormatted
        {
            get
            {
                if (WastedSpace < 1024) return $"{WastedSpace} B";
                if (WastedSpace < 1024 * 1024) return $"{WastedSpace / 1024.0:F2} KB";
                if (WastedSpace < 1024 * 1024 * 1024) return $"{WastedSpace / (1024.0 * 1024.0):F2} MB";
                return $"{WastedSpace / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }
        
        public string GroupDisplayName
        {
            get
            {
                if (Files != null && Files.Count > 0)
                {
                    var fileName = System.IO.Path.GetFileName(Files[0]);
                    return $"{fileName} ({FileCount} copies, {WastedSpaceFormatted} wasted)";
                }
                return $"Group ({FileCount} files)";
            }
        }
    }
    
    /// <summary>
    /// Represents an individual duplicate file within a group
    /// </summary>
    public class DuplicateFile : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isRecommendedKeep;
        
        public string FilePath { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public long FileSize { get; set; }
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }
        
        public bool IsRecommendedKeep
        {
            get => _isRecommendedKeep;
            set
            {
                if (_isRecommendedKeep != value)
                {
                    _isRecommendedKeep = value;
                    OnPropertyChanged(nameof(IsRecommendedKeep));
                    OnPropertyChanged(nameof(DisplayIcon));
                }
            }
        }
        
        public string DisplayIcon => IsRecommendedKeep ? "⭐" : "";
        
        public string DisplayPath => FilePath;
        
        public string DisplayDates => $"Created: {CreatedDate:MMM d, yyyy}  |  Modified: {ModifiedDate:MMM d, yyyy}";
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}