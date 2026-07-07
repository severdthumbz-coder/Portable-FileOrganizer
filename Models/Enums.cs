namespace FileOrganizer.Models
{
    /// <summary>
    /// File operation mode
    /// </summary>
    public enum FileOperationMode
    {
        /// <summary>
        /// Transfers files to destination and removes from source
        /// </summary>
        Move,
        
        /// <summary>
        /// Duplicates files to destination, keeps originals in source
        /// </summary>
        Copy
    }

    /// <summary>
    /// Destination folder structure organization mode
    /// </summary>
    public enum DestinationStructureMode
    {
        /// <summary>
        /// Organizes into category folders (Documents, Images, etc.) with optional date subfolders
        /// </summary>
        OrganizeByCategory,
        
        /// <summary>
        /// Maintains exact source folder hierarchy at destination (Recommended)
        /// </summary>
        PreserveStructure,
        
        /// <summary>
        /// Organizes by category first, then preserves relative subfolder structure
        /// </summary>
        Hybrid
    }

    /// <summary>
    /// File conflict resolution strategy
    /// </summary>
    public enum FileConflictResolution
    {
        /// <summary>
        /// Skip files that already exist
        /// </summary>
        Skip,
        
        /// <summary>
        /// Overwrite existing files
        /// </summary>
        Overwrite,
        
        /// <summary>
        /// Only overwrite if source file is newer
        /// </summary>
        OverwriteIfNewer,
        
        /// <summary>
        /// Rename and keep both files
        /// </summary>
        RenameKeepBoth
    }

    /// <summary>
    /// Exception type for file/folder exclusions
    /// </summary>
    public enum ExceptionType
    {
        /// <summary>
        /// Completely ignores matching files/folders (not copied, not removed)
        /// </summary>
        Exclude,
        
        /// <summary>
        /// Folder structure preserved but only contents duplicated
        /// </summary>
        Semi
    }
}
