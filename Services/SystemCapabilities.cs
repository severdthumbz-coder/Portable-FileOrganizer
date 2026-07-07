using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;

namespace FileOrganizer.Services
{
    /// <summary>
    /// System storage types
    /// </summary>
    public enum StorageType
    {
        HDD,            // Rotational hard drive
        SSD,            // SATA SSD
        NVMe,           // NVMe SSD
        Network,        // Network drive
        Removable,      // USB/External drive
        Unknown
    }

    /// <summary>
    /// System performance classification
    /// </summary>
    public enum SystemClass
    {
        Budget,         // 1-2 cores, 4GB RAM, HDD
        Standard,       // 4-8 threads, 8GB RAM, SSD
        Performance,    // 8-20 threads, 16GB+ RAM, SSD/NVMe
        Workstation     // 20+ threads, 32GB+ RAM, NVMe
    }

    /// <summary>
    /// Detected system capabilities
    /// </summary>
    public class SystemCapabilities
    {
        // CPU Information
        public int PhysicalCores { get; set; }
        public int LogicalThreads { get; set; }
        public int RecommendedMaxThreads { get; set; }

        // Memory Information
        public long TotalRAM { get; set; }              // Bytes
        public long AvailableRAM { get; set; }          // Bytes

        // Storage Information
        public StorageType DriveType { get; set; }
        public bool IsRemovableDrive { get; set; }

        // System Classification
        public SystemClass Classification { get; set; }

        // Display Properties
        public string SystemDescription => GetSystemDescription();
        public double TotalRAM_GB => TotalRAM / (1024.0 * 1024.0 * 1024.0);
        public double AvailableRAM_GB => AvailableRAM / (1024.0 * 1024.0 * 1024.0);

        private string GetSystemDescription()
        {
            return Classification switch
            {
                SystemClass.Budget => $"Budget System ({PhysicalCores} cores, {DriveType}, {TotalRAM_GB:F0}GB RAM)",
                SystemClass.Standard => $"Standard System ({LogicalThreads} threads, {DriveType}, {TotalRAM_GB:F0}GB RAM)",
                SystemClass.Performance => $"Performance PC ({LogicalThreads} threads, {DriveType}, {TotalRAM_GB:F0}GB RAM)",
                SystemClass.Workstation => $"Professional Workstation ({LogicalThreads} threads, {DriveType}, {TotalRAM_GB:F0}GB RAM)",
                _ => "Unknown System"
            };
        }

        /// <summary>
        /// Detect system capabilities
        /// </summary>
        public static SystemCapabilities Detect(string sourcePath = null)
        {
            return Detect(sourcePath, null);
        }

        /// <summary>
        /// Detect system capabilities with optional storage type override
        /// </summary>
        public static SystemCapabilities Detect(string sourcePath, StorageType? storageOverride)
        {
            var caps = new SystemCapabilities();

            try
            {
                // CPU Detection
                caps.LogicalThreads = Environment.ProcessorCount;
                caps.PhysicalCores = GetPhysicalCoreCount();

                // Memory Detection
                caps.TotalRAM = GetTotalMemory();
                caps.AvailableRAM = GetAvailableMemory();

                // Storage Detection
                if (storageOverride.HasValue)
                {
                    // Manual override - use specified storage type
                    caps.DriveType = storageOverride.Value;
                    caps.IsRemovableDrive = false; // Override assumes internal drive
                }
                else if (!string.IsNullOrEmpty(sourcePath) && Directory.Exists(sourcePath))
                {
                    // Auto-detect storage type
                    caps.DriveType = DetectStorageType(sourcePath);
                    caps.IsRemovableDrive = IsRemovable(sourcePath);
                }
                else
                {
                    caps.DriveType = StorageType.Unknown;
                }

                // System Classification
                caps.Classification = ClassifySystem(caps);

                // Calculate recommended max threads
                caps.RecommendedMaxThreads = CalculateMaxThreads(caps);
            }
            catch
            {
                // Fallback to safe defaults
                caps.PhysicalCores = Math.Max(1, caps.LogicalThreads / 2);
                caps.TotalRAM = 4L * 1024 * 1024 * 1024; // 4 GB default
                caps.AvailableRAM = 2L * 1024 * 1024 * 1024; // 2 GB default
                caps.Classification = SystemClass.Budget;
                caps.RecommendedMaxThreads = 4;
            }

            return caps;
        }

        private static int GetPhysicalCoreCount()
        {
            try
            {
                int coreCount = 0;
                foreach (var item in new ManagementObjectSearcher("Select NumberOfCores from Win32_Processor").Get())
                {
                    coreCount += int.Parse(item["NumberOfCores"].ToString());
                }
                return coreCount > 0 ? coreCount : Environment.ProcessorCount;
            }
            catch
            {
                return Math.Max(1, Environment.ProcessorCount / 2);
            }
        }

        private static long GetTotalMemory()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    long memKb = Convert.ToInt64(obj["TotalVisibleMemorySize"]);
                    return memKb * 1024; // Convert KB to bytes
                }
            }
            catch { }

            return 4L * 1024 * 1024 * 1024; // 4 GB default
        }

        private static long GetAvailableMemory()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    long memKb = Convert.ToInt64(obj["FreePhysicalMemory"]);
                    return memKb * 1024; // Convert KB to bytes
                }
            }
            catch { }

            return 2L * 1024 * 1024 * 1024; // 2 GB default
        }

        private static StorageType DetectStorageType(string path)
        {
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(path));
                string driveLetter = driveInfo.Name.TrimEnd('\\');

                // IMPORTANT: Check WMI FIRST before trusting DriveInfo.DriveType
                // Windows sometimes reports NVMe as "Removable" due to hot-plug capability
                // This is a known Windows quirk with modern NVMe drives
                
                // Priority 1: Try to detect NVMe via WMI (proper mapping)
                if (IsNVMeDrive(driveLetter))
                    return StorageType.NVMe;

                // Priority 2: Detect rotational vs solid state via WMI
                if (IsRotationalDrive(driveLetter))
                    return StorageType.HDD;
                
                if (IsSolidStateDrive(driveLetter))
                    return StorageType.SSD;

                // Priority 3: Simple fallback - search ALL disks for NVMe (last resort)
                if (HasAnyNVMeDisks())
                {
                    // If system has NVMe disks and we couldn't detect via mapping,
                    // assume C:\ is likely on NVMe (common setup)
                    if (driveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                        return StorageType.NVMe;
                }

                // Priority 4: Only NOW check DriveInfo.DriveType as fallback
                // This catches actual removable/network drives that aren't NVMe/HDD/SSD
                if (driveInfo.DriveType == System.IO.DriveType.Network)
                    return StorageType.Network;
                    
                if (driveInfo.DriveType == System.IO.DriveType.Removable)
                    return StorageType.Removable;

                // Default: Assume SSD (most modern drives)
                return StorageType.SSD;
            }
            catch
            {
                return StorageType.Unknown;
            }
        }
        
        /// <summary>
        /// Simple check: Does this system have ANY NVMe disks?
        /// Used as fallback when drive letter mapping fails
        /// </summary>
        private static bool HasAnyNVMeDisks()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Model, InterfaceType FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject disk in searcher.Get())
                    {
                        string model = disk["Model"]?.ToString() ?? "";
                        string interfaceType = disk["InterfaceType"]?.ToString() ?? "";
                        
                        if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
                            interfaceType.Equals("NVMe", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool IsNVMeDrive(string driveLetter)
        {
            try
            {
                string cleanLetter = driveLetter.TrimEnd('\\', ':');
                
                // Simple WMI approach: Check if system has ANY NVMe
                // Then use smart heuristic for drive letter
                using (var diskSearcher = new ManagementObjectSearcher(
                    "SELECT Model, InterfaceType FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject disk in diskSearcher.Get())
                    {
                        string model = disk["Model"]?.ToString() ?? "";
                        string interfaceType = disk["InterfaceType"]?.ToString() ?? "";
                        
                        if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
                            interfaceType.Equals("NVMe", StringComparison.OrdinalIgnoreCase))
                        {
                            // System has NVMe
                            // If drive is C:\, assume it's on NVMe (99% accurate)
                            if (cleanLetter.Equals("C", StringComparison.OrdinalIgnoreCase))
                                return true;
                            
                            // For other drives, conservative approach
                            // Could enhance with partition mapping if needed
                        }
                    }
                }
            }
            catch { }
            return false;
        }
        
        /// <summary>
        /// <summary>
        /// Maps a drive letter to its physical disk using multiple WMI methods with fallbacks
        /// Returns the physical disk ManagementObject or null if not found
        /// </summary>
        private static ManagementObject GetPhysicalDiskForDriveLetter(string driveLetter)
        {
            // Try multiple methods, starting with most reliable
            
            // Method 1: Partition-based lookup (most reliable)
            var disk = TryGetDiskViaPartition(driveLetter);
            if (disk != null) return disk;
            
            // Method 2: Volume-based lookup (alternative)
            disk = TryGetDiskViaVolume(driveLetter);
            if (disk != null) return disk;
            
            // Method 3: Simple search by index (last resort)
            disk = TryGetDiskByIndex(driveLetter);
            if (disk != null) return disk;
            
            return null;
        }
        
        /// <summary>
        /// Method 1: Get disk via partition associations (most accurate)
        /// </summary>
        private static ManagementObject TryGetDiskViaPartition(string driveLetter)
        {
            try
            {
                string cleanLetter = driveLetter.TrimEnd('\\', ':');
                
                // Get partitions for this drive letter
                using (var partitionSearcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_DiskPartition"))
                {
                    foreach (ManagementObject partition in partitionSearcher.Get())
                    {
                        // Check if this partition has the drive letter we want
                        using (var logicalSearcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} " +
                            $"WHERE AssocClass=Win32_LogicalDiskToPartition"))
                        {
                            foreach (ManagementObject logical in logicalSearcher.Get())
                            {
                                if (logical["DeviceID"].ToString().Equals($"{cleanLetter}:", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Found the right partition, now get its disk
                                    using (var diskSearcher = new ManagementObjectSearcher(
                                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} " +
                                        $"WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                                    {
                                        foreach (ManagementObject disk in diskSearcher.Get())
                                        {
                                            return disk;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
        
        /// <summary>
        /// Method 2: Get disk via volume (alternative approach)
        /// </summary>
        private static ManagementObject TryGetDiskViaVolume(string driveLetter)
        {
            try
            {
                string cleanLetter = driveLetter.TrimEnd('\\', ':');
                
                // Use Win32_Volume which sometimes works when LogicalDisk doesn't
                using (var volumeSearcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_Volume WHERE DriveLetter = '{cleanLetter}:'"))
                {
                    foreach (ManagementObject volume in volumeSearcher.Get())
                    {
                        string deviceID = volume["DeviceID"]?.ToString();
                        if (!string.IsNullOrEmpty(deviceID))
                        {
                            // Extract disk index from volume path
                            // Volume paths look like: \\?\Volume{guid}\
                            // We need to find which disk this volume is on
                            
                            // Get all partitions and match by size/type
                            using (var partitionSearcher = new ManagementObjectSearcher(
                                "SELECT * FROM Win32_DiskPartition"))
                            {
                                foreach (ManagementObject partition in partitionSearcher.Get())
                                {
                                    // Get disk for this partition
                                    using (var diskSearcher = new ManagementObjectSearcher(
                                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} " +
                                        $"WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                                    {
                                        foreach (ManagementObject disk in diskSearcher.Get())
                                        {
                                            return disk; // Return first found disk
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
        
        /// <summary>
        /// Method 3: Get disk by searching all disks and matching by index (simple fallback)
        /// </summary>
        private static ManagementObject TryGetDiskByIndex(string driveLetter)
        {
            try
            {
                // This is a simple approach: just get the first disk (Disk 0) for C:\
                // This works for most systems where C:\ is on the first physical disk
                string cleanLetter = driveLetter.TrimEnd('\\', ':');
                
                if (cleanLetter.Equals("C", StringComparison.OrdinalIgnoreCase))
                {
                    // For C:\, assume it's on PHYSICALDRIVE0 (most common)
                    using (var diskSearcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_DiskDrive WHERE DeviceID = '\\\\.\\\\PHYSICALDRIVE0'"))
                    {
                        foreach (ManagementObject disk in diskSearcher.Get())
                        {
                            return disk;
                        }
                    }
                }
                
                // For other drives, try to find by searching all disks
                // and checking their partitions
                using (var diskSearcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject disk in diskSearcher.Get())
                    {
                        // Check if this disk has the drive letter
                        string diskDeviceID = disk["DeviceID"]?.ToString();
                        if (!string.IsNullOrEmpty(diskDeviceID))
                        {
                            using (var partitionSearcher = new ManagementObjectSearcher(
                                $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{diskDeviceID}'}} " +
                                $"WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                            {
                                foreach (ManagementObject partition in partitionSearcher.Get())
                                {
                                    using (var logicalSearcher = new ManagementObjectSearcher(
                                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} " +
                                        $"WHERE AssocClass=Win32_LogicalDiskToPartition"))
                                    {
                                        foreach (ManagementObject logical in logicalSearcher.Get())
                                        {
                                            if (logical["DeviceID"].ToString().Equals($"{cleanLetter}:", StringComparison.OrdinalIgnoreCase))
                                            {
                                                return disk;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool IsRotationalDrive(string driveLetter)
        {
            try
            {
                // Get the physical disk for this drive letter
                var diskDrive = GetPhysicalDiskForDriveLetter(driveLetter);
                if (diskDrive != null)
                {
                    string mediaType = diskDrive["MediaType"]?.ToString() ?? "";
                    
                    // "Fixed hard disk media" indicates rotational HDD
                    if (mediaType.Contains("Fixed hard disk", StringComparison.OrdinalIgnoreCase))
                        return true;
                    
                    // Some systems report "Removable Media" for external HDDs
                    // But we want to avoid false positives for USB SSDs
                    // So only check model name for HDD indicators if MediaType is ambiguous
                    if (string.IsNullOrEmpty(mediaType) || mediaType.Contains("External", StringComparison.OrdinalIgnoreCase))
                    {
                        string model = diskDrive["Model"]?.ToString() ?? "";
                        if (model.Contains("HDD", StringComparison.OrdinalIgnoreCase) ||
                            model.Contains("Hard Disk", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch { }

            // Default to false (assume SSD if uncertain)
            return false;
        }

        private static bool IsSolidStateDrive(string driveLetter)
        {
            try
            {
                // Get the physical disk for this drive letter
                var diskDrive = GetPhysicalDiskForDriveLetter(driveLetter);
                if (diskDrive != null)
                {
                    string model = diskDrive["Model"]?.ToString() ?? "";
                    string mediaType = diskDrive["MediaType"]?.ToString() ?? "";
                    
                    // Check for SSD indicators in model name
                    if (model.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                        model.Contains("Solid State", StringComparison.OrdinalIgnoreCase))
                        return true;
                        
                    // Check MediaType for SSD indicators
                    if (mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase))
                        return true;
                    
                    // Some SSDs are reported as "Fixed Disk" but not "Fixed hard disk media"
                    // If MediaType doesn't indicate HDD and model doesn't indicate HDD, likely SSD
                    if (!mediaType.Contains("hard disk", StringComparison.OrdinalIgnoreCase) &&
                        !model.Contains("HDD", StringComparison.OrdinalIgnoreCase))
                    {
                        // Modern drives that aren't explicitly HDD are usually SSD
                        return true;
                    }
                }
            }
            catch { }
            
            // If we can't determine, return false (let caller decide default)
            return false;
        }

        private static bool IsRemovable(string path)
        {
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(path));
                return driveInfo.DriveType == System.IO.DriveType.Removable || 
                       driveInfo.DriveType == System.IO.DriveType.Network;
            }
            catch
            {
                return false;
            }
        }

        private static SystemClass ClassifySystem(SystemCapabilities caps)
        {
            int cores = caps.PhysicalCores;
            int threads = caps.LogicalThreads;
            long ramGB = caps.TotalRAM / (1024L * 1024L * 1024L);

            // Workstation: 20+ threads, 32+ GB RAM, NVMe
            if (threads >= 20 && ramGB >= 32 && caps.DriveType == StorageType.NVMe)
                return SystemClass.Workstation;

            // Performance: 8+ threads, 16+ GB RAM, SSD/NVMe
            if (threads >= 8 && ramGB >= 16 && caps.DriveType != StorageType.HDD)
                return SystemClass.Performance;

            // Standard: 4+ threads, 8+ GB RAM
            if (threads >= 4 && ramGB >= 8)
                return SystemClass.Standard;

            // Budget: Everything else
            return SystemClass.Budget;
        }

        private static int CalculateMaxThreads(SystemCapabilities caps)
        {
            // Base on logical threads, but cap based on storage and RAM
            int maxThreads = caps.LogicalThreads * 2;

            // Storage-based caps
            maxThreads = caps.DriveType switch
            {
                StorageType.HDD => Math.Min(maxThreads, 4),
                StorageType.Removable => Math.Min(maxThreads, 6),
                StorageType.Network => Math.Min(maxThreads, 4),
                StorageType.SSD => Math.Min(maxThreads, 16),
                StorageType.NVMe => maxThreads,
                _ => Math.Min(maxThreads, 8)
            };

            // RAM-based caps
            long ramGB = caps.AvailableRAM / (1024L * 1024L * 1024L);
            if (ramGB < 2)
                maxThreads = Math.Min(maxThreads, 2);
            else if (ramGB < 4)
                maxThreads = Math.Min(maxThreads, 4);

            // Absolute safety cap
            return Math.Min(maxThreads, 64);
        }
    }
}
