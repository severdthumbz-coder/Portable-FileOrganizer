using System.Collections.Generic;

namespace FileOrganizer.Models
{
    /// <summary>
    /// Application configuration settings
    /// </summary>
    public class Config
    {
        public ScanMode ScanMode { get; set; } = ScanMode.Auto;
        public CopyEngine CopyEngine { get; set; } = CopyEngine.CustomFast;
        public FileOperationMode OperationMode { get; set; } = FileOperationMode.Move;
        public DestinationStructureMode StructureMode { get; set; } = DestinationStructureMode.PreserveStructure;
        public FileConflictResolution ConflictResolution { get; set; } = FileConflictResolution.Skip;
        
        public string SourceFolder { get; set; } = string.Empty;
        public List<string> SourceFolders { get; set; } = new List<string>();
        public bool UseMultipleSources { get; set; } = false;
        
        public string DestinationFolder { get; set; } = string.Empty;
        
        public bool EnableDateOrganization { get; set; } = false;
        public string DateFormat { get; set; } = "Year\\Month (2024\\02)"; // Default format
        
        // File timestamp preservation
        public bool PreserveTimestamps { get; set; } = true; // Default to TRUE
        
        // Data integrity verification
        public VerificationMode VerificationMode { get; set; } = VerificationMode.Smart; // Default to Smart (recommended)

        // When using external engines (TeraCopy / FastCopy), verify each copied file.
        // Enables the tool's own verification (FastCopy /verify, TeraCopy CRC) plus an
        // app-side destination existence + size check. If VerificationMode is FullHash,
        // an independent SHA-256 comparison is also performed. Default ON for safety.
        public bool VerifyExternalCopies { get; set; } = true;
        
        // Error recovery settings
        public bool ContinueOnErrors { get; set; } = true;
        public int RetryAttempts { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 2;
        
        // Storage type override (manual selection)
        public string StorageOverride { get; set; } = "Auto"; // "Auto", "NVMe", "SSD", "HDD"
        
        // Exception filters
        public List<ExceptionFilter> Exceptions { get; set; } = new List<ExceptionFilter>();

        // ---- Automation (Tier 1): rule engine, folder watching, scheduling ----
        public List<OrganizationRule> Rules { get; set; } = new List<OrganizationRule>();

        // Folders the watcher/scheduler operate on. Falls back to SourceFolders if empty.
        public List<string> WatchFolders { get; set; } = new List<string>();
        public bool WatchIncludeSubfolders { get; set; } = false;

        // Scheduler
        public bool ScheduleEnabled { get; set; } = false;
        public int ScheduleIntervalMinutes { get; set; } = 60;
        public bool ScheduleRunOnStart { get; set; } = false;
        
        // External tool paths
        public string TeraCopyPath { get; set; } = @"C:\Program Files\TeraCopy\TeraCopy.exe";
        public string FastCopyPath { get; set; } = @"C:\Program Files\FastCopy\FastCopy.exe";
        
        public Config()
        {
            SourceFolders = new List<string>();
            Exceptions = new List<ExceptionFilter>();
        }
    }
}
