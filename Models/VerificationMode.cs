namespace FileOrganizer.Models
{
    /// <summary>
    /// Data integrity verification modes for file operations
    /// </summary>
    public enum VerificationMode
    {
        /// <summary>
        /// No verification - fastest but risky (not recommended)
        /// </summary>
        None,

        /// <summary>
        /// Quick size comparison only - fast and catches most issues
        /// </summary>
        SizeOnly,

        /// <summary>
        /// Smart mode - hash small files, size check large files (recommended)
        /// Best balance of speed and safety
        /// </summary>
        Smart,

        /// <summary>
        /// Full hash verification for all files - slowest but 100% safe
        /// Doubles operation time but guarantees data integrity
        /// </summary>
        FullHash
    }
}
