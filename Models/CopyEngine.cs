namespace FileOrganizer.Models
{
    /// <summary>
    /// Available copy/move engines
    /// </summary>
    public enum CopyEngine
    {
        /// <summary>
        /// Windows built-in copy API - reliable but slower
        /// </summary>
        WindowsStandard,
        
        /// <summary>
        /// Multi-threaded buffered I/O with real-time progress (Recommended)
        /// </summary>
        CustomFast,
        
        /// <summary>
        /// Requires TeraCopy installed - best for large files with verification
        /// </summary>
        TeraCopy,
        
        /// <summary>
        /// Requires FastCopy installed - extremely fast for large batch operations
        /// </summary>
        FastCopy
    }
}
