using System;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Manages adaptive performance based on system capabilities
    /// </summary>
    public class AdaptivePerformanceManager
    {
        private SystemCapabilities _capabilities;
        private static AdaptivePerformanceManager _instance;
        private static readonly object _lock = new object();
        private StorageType? _storageOverride = null; // Manual override (null = auto-detect)

        public SystemCapabilities Capabilities => _capabilities;
        public StorageType? StorageOverride => _storageOverride;

        private AdaptivePerformanceManager()
        {
            _capabilities = SystemCapabilities.Detect();
        }

        /// <summary>
        /// Get singleton instance
        /// </summary>
        public static AdaptivePerformanceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AdaptivePerformanceManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Set manual storage type override (null = auto-detect)
        /// </summary>
        public void SetStorageOverride(StorageType? storageType)
        {
            _storageOverride = storageType;
            
            // If override is set, apply it immediately
            if (_storageOverride.HasValue)
            {
                _capabilities = SystemCapabilities.Detect(null, _storageOverride.Value);
            }
            else
            {
                // Revert to auto-detect
                _capabilities = SystemCapabilities.Detect();
            }
        }

        /// <summary>
        /// Refresh system capabilities (call if source path changes)
        /// </summary>
        public void RefreshCapabilities(string sourcePath = null)
        {
            // If manual override is active, use it
            if (_storageOverride.HasValue)
            {
                _capabilities = SystemCapabilities.Detect(sourcePath, _storageOverride.Value);
            }
            else
            {
                _capabilities = SystemCapabilities.Detect(sourcePath);
            }
        }

        /// <summary>
        /// Get optimal thread count for scanning operation
        /// </summary>
        public int GetOptimalThreadCount(ScanMode mode, int fileCount = 0, string sourcePath = null)
        {
            // Refresh capabilities if source path provided
            if (!string.IsNullOrEmpty(sourcePath) && _capabilities.DriveType == StorageType.Unknown)
            {
                RefreshCapabilities(sourcePath);
            }

            // Get base thread count
            int baseThreads = GetBaseThreadCount(mode);

            // Apply system-specific adjustments
            int adjustedThreads = ApplySystemAdjustments(baseThreads);

            // Apply storage-specific adjustments
            int finalThreads = ApplyStorageAdjustments(adjustedThreads);

            // Apply file count scaling (for Auto mode)
            if (mode == ScanMode.Auto && fileCount > 0)
            {
                finalThreads = ApplyFileCountScaling(finalThreads, fileCount);
            }

            // Apply safety caps
            return ApplySafetyCaps(finalThreads);
        }

        /// <summary>
        /// Get thread count for specific scan mode (for UI display)
        /// </summary>
        public int GetThreadCountForMode(ScanMode mode)
        {
            return GetOptimalThreadCount(mode, 0, null);
        }

        /// <summary>
        /// Get thread count range for Auto mode (for UI display)
        /// </summary>
        public (int min, int max) GetAutoModeRange()
        {
            int minThreads = GetOptimalThreadCount(ScanMode.Auto, 10, null);  // Small file count
            int maxThreads = GetOptimalThreadCount(ScanMode.Auto, 100000, null);  // Large file count
            return (minThreads, maxThreads);
        }

        /// <summary>
        /// Get description for scan mode based on system capabilities
        /// </summary>
        public string GetScanModeDescription(ScanMode mode)
        {
            int threads = GetThreadCountForMode(mode);
            string storageNote = GetStorageAssumptionNote();

            return mode switch
            {
                ScanMode.Normal => $"Uses {threads} {Plural(threads, "thread")}. Best for everyday scanning.{storageNote}",
                ScanMode.Fast => $"Uses {threads} {Plural(threads, "thread")}. Faster scanning with higher CPU usage.{storageNote}",
                ScanMode.Turbo => $"Uses {threads} {Plural(threads, "thread")}. Maximum speed for your system. Best for large folders.{storageNote}",
                ScanMode.Auto => GetAutoModeDescription() + storageNote,
                _ => $"Uses {threads} {Plural(threads, "thread")}.{storageNote}"
            };
        }
        
        private string GetStorageAssumptionNote()
        {
            if (_capabilities.DriveType == StorageType.Unknown)
            {
                // Indicate we're making an assumption based on system class
                return _capabilities.Classification switch
                {
                    SystemClass.Workstation or SystemClass.Performance => 
                        " (Assuming SSD - will verify when folder selected)",
                    SystemClass.Standard => 
                        " (Assuming SSD - will verify when folder selected)",
                    SystemClass.Budget => 
                        " (Assuming HDD for safety - will verify when folder selected)",
                    _ => " (Storage unknown - will detect when folder selected)"
                };
            }
            return string.Empty; // Storage type is known, no note needed
        }

        private string GetAutoModeDescription()
        {
            var (min, max) = GetAutoModeRange();
            if (min == max)
                return $"Adapts to file count (uses {min} {Plural(min, "thread")}).";
            return $"Adapts to file count ({min}-{max} threads based on folder size).";
        }

        private string Plural(int count, string word)
        {
            return count == 1 ? word : word + "s";
        }

        private int GetBaseThreadCount(ScanMode mode)
        {
            int cores = _capabilities.PhysicalCores;
            int threads = _capabilities.LogicalThreads;

            return mode switch
            {
                // Normal: Conservative (50% of logical threads)
                ScanMode.Normal => Math.Max(1, threads / 2),

                // Fast: Balanced (100% of logical threads)
                ScanMode.Fast => threads,

                // Turbo: Aggressive (200% of logical threads)
                ScanMode.Turbo => threads * 2,

                // Auto: Start with 100% (will be scaled by file count)
                ScanMode.Auto => threads,

                _ => Math.Max(2, cores)
            };
        }

        private int ApplySystemAdjustments(int baseThreads)
        {
            long availableRAM_GB = _capabilities.AvailableRAM / (1024L * 1024L * 1024L);

            // Reduce threads if low on RAM
            if (availableRAM_GB < 2)
            {
                return Math.Min(baseThreads, 2);
            }
            else if (availableRAM_GB < 4)
            {
                return Math.Min(baseThreads, 4);
            }

            return baseThreads;
        }

        private int ApplyStorageAdjustments(int baseThreads)
        {
            return _capabilities.DriveType switch
            {
                // HDD: Severely limit threads (random I/O kills performance)
                StorageType.HDD => Math.Min(baseThreads, 4),

                // Network/Removable: Conservative (latency/bandwidth limited)
                StorageType.Network => Math.Min(baseThreads, 4),
                StorageType.Removable => Math.Min(baseThreads, 6),

                // SATA SSD: Moderate (good but limited queue depth)
                StorageType.SSD => Math.Min(baseThreads, 16),

                // NVMe: Full performance (high queue depth, very fast)
                StorageType.NVMe => baseThreads,

                // Unknown: Smart defaults based on system class
                _ => GetDefaultThreadsForUnknownStorage(baseThreads)
            };
        }
        
        /// <summary>
        /// Provides intelligent thread count defaults when storage type is unknown
        /// Uses system classification to make safe assumptions
        /// </summary>
        private int GetDefaultThreadsForUnknownStorage(int baseThreads)
        {
            // Use system classification for intelligent defaults
            switch (_capabilities.Classification)
            {
                case SystemClass.Workstation:
                case SystemClass.Performance:
                    // Modern high-end systems (16+ threads, 16GB+ RAM) 
                    // Almost always have SSD or NVMe
                    // Safe to assume SSD-level performance
                    return Math.Min(baseThreads, 16);  // SSD cap
                    
                case SystemClass.Standard:
                    // Standard systems (8 threads, 8GB RAM)
                    // Most modern systems have SSD today
                    // Assume SSD but be reasonable
                    return Math.Min(baseThreads, 16);  // SSD cap
                    
                case SystemClass.Budget:
                    // Budget systems (2-4 threads, 4-8GB RAM)
                    // Often still have HDD, especially older laptops
                    // Be conservative to avoid thrashing!
                    return Math.Min(baseThreads, 4);   // HDD cap (safe)
                    
                default:
                    // Fallback: moderate middle ground
                    return Math.Min(baseThreads, 8);
            }
        }

        private int ApplyFileCountScaling(int baseThreads, int fileCount)
        {
            // Don't spin up many threads for small jobs
            if (fileCount < 10) return 1;
            if (fileCount < 100) return Math.Min(2, baseThreads);
            if (fileCount < 1000) return Math.Min(4, baseThreads);
            if (fileCount < 10000) return Math.Min(baseThreads / 2, baseThreads);

            return baseThreads;  // Full power for large jobs
        }

        private int ApplySafetyCaps(int threads)
        {
            // Never exceed system capabilities by too much
            int maxThreads = _capabilities.LogicalThreads * 3;

            // Never exceed reasonable absolute limit
            maxThreads = Math.Min(maxThreads, 64);

            // Never go below 1
            threads = Math.Max(1, threads);

            return Math.Min(threads, maxThreads);
        }
    }
}
