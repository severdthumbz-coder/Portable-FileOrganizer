using System;
using System.Collections.Generic;

namespace FileOrganizer.Models
{
    /// <summary>
    /// Represents a file in the processing queue
    /// </summary>
    public class QueueEntry
    {
        public string FileName { get; set; }
        public string Category { get; set; }
        public long SizeBytes { get; set; }
        public string Status { get; set; } = "Pending";
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        
        // Verification tracking
        public bool Verified { get; set; } = false;
        public string VerificationMethod { get; set; } = "Pending";
        public int VerificationRetries { get; set; } = 0;
        public bool VerificationFailed { get; set; } = false;
        
        // Exception handling
        public bool IsSemiExcluded { get; set; } = false;
        
        public string SizeFormatted
        {
            get
            {
                if (SizeBytes < 1024) return $"{SizeBytes} B";
                if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F2} KB";
                if (SizeBytes < 1024 * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024.0):F2} MB";
                return $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }
    }

    /// <summary>
    /// Represents a historical operation entry
    /// </summary>
    public class HistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Mode { get; set; }  // Move or Copy
        public int FilesScanned { get; set; }
        public int SuccessCount { get; set; }
        public string Status { get; set; }

        // Re-run support (Tier 2): remember what the operation acted on.
        public string SourceFolder { get; set; } = "";
        public string DestinationFolder { get; set; } = "";
        public bool CanReRun => !string.IsNullOrWhiteSpace(SourceFolder) && !string.IsNullOrWhiteSpace(DestinationFolder);
        
        // Verification tracking
        public int FilesVerified { get; set; } = 0;
        public int VerificationPassed { get; set; } = 0;
        public int VerificationFailed { get; set; } = 0;
        public int VerificationRetried { get; set; } = 0;
        
        public string TimestampFormatted => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        
        public string VerificationStatusDisplay
        {
            get
            {
                if (FilesVerified == 0)
                    return "No verification";
                
                if (VerificationFailed == 0)
                    return $"✅ {VerificationPassed}/{FilesVerified} (100%)";
                else if (VerificationFailed > 0 && VerificationPassed == FilesVerified - VerificationFailed)
                    return $"⚠️ {VerificationPassed}/{FilesVerified} ({(double)VerificationPassed/FilesVerified*100:F1}%)";
                else
                    return $"❌ {VerificationPassed}/{FilesVerified} ({(double)VerificationPassed/FilesVerified*100:F1}%)";
            }
        }
    }

    /// <summary>
    /// Represents an exception filter for excluding files/folders
    /// </summary>
    public class ExceptionFilter
    {
        public bool IsEnabled { get; set; } = true;
        public string Path { get; set; }
        public bool IsFolder { get; set; }
        public ExceptionType Type { get; set; } = ExceptionType.Exclude;
        
        public string TypeDisplay => Type == ExceptionType.Exclude ? "Exclude" : "Semi";
    }

    /// <summary>
    /// Resume state for interrupted operations
    /// </summary>
    public class ResumeState
    {
        public DateTime InterruptedAt { get; set; }
        public FileOperationMode OperationMode { get; set; }
        public string SourceFolder { get; set; }
        public string DestinationFolder { get; set; }
        public List<string> CompletedFiles { get; set; } = new List<string>();
        public List<QueueEntry> RemainingQueue { get; set; } = new List<QueueEntry>();
        public int TotalFiles { get; set; }
        public int CompletedCount { get; set; }
    }
}
