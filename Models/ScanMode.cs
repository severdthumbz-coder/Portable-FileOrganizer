namespace FileOrganizer.Models
{
    /// <summary>
    /// Scan mode options for file scanning performance
    /// </summary>
    public enum ScanMode
    {
        /// <summary>
        /// Automatically selects best method based on folder size
        /// </summary>
        Auto,
        
        /// <summary>
        /// 2-4 threads, best for less than 10K files
        /// </summary>
        Normal,
        
        /// <summary>
        /// 4-8 threads, best for 10K-50K files
        /// </summary>
        Fast,
        
        /// <summary>
        /// 8-16 threads, best for more than 50K files
        /// </summary>
        Turbo
    }
}
