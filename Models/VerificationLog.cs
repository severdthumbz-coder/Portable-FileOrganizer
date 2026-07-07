using System;

namespace FileOrganizer.Models
{
    /// <summary>
    /// Log entry for file verification events
    /// </summary>
    public class VerificationLog
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public DateTime Timestamp { get; set; }
        public bool Passed { get; set; }
        public VerificationMode VerificationMode { get; set; }
        public string SourceHash { get; set; }
        public string DestHash { get; set; }
        public long FileSize { get; set; }
        public int RetryCount { get; set; }
        public string FailureReason { get; set; }
        public TimeSpan VerificationDuration { get; set; }

        public string FileName => System.IO.Path.GetFileName(SourcePath);
        
        public string VerificationMethodDisplay
        {
            get
            {
                if (VerificationMode == VerificationMode.None)
                    return "None";
                else if (VerificationMode == VerificationMode.SizeOnly)
                    return "Size Check";
                else if (!string.IsNullOrEmpty(SourceHash))
                    return "SHA256 Hash";
                else
                    return "Size Check";
            }
        }

        public string StatusDisplay
        {
            get
            {
                if (Passed)
                {
                    if (RetryCount > 0)
                        return $"✅ Verified (Retry {RetryCount})";
                    else
                        return "✅ Verified";
                }
                else
                {
                    return $"❌ Failed ({RetryCount} retries)";
                }
            }
        }
    }
}
