using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Smart router that delegates to the appropriate copy engine
    /// </summary>
    public class MoveEngine
    {
        private readonly Config _config;
        private CustomFastCopyEngine _customEngine;
        private TeraCopyEngine _teraCopyEngine;
        private FastCopyEngine _fastCopyEngine;

        public MoveEngine(Config config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            InitializeEngines();
        }

        private void InitializeEngines()
        {
            // Always initialize custom engine
            _customEngine = new CustomFastCopyEngine();

            // Initialize external engines if available
            if (_config.CopyEngine == CopyEngine.TeraCopy)
            {
                var teraCopyPath = EngineDetector.DetectTeraCopy();
                if (teraCopyPath.IsInstalled)
                {
                    try
                    {
                        _teraCopyEngine = new TeraCopyEngine(
                            teraCopyPath.InstallPath, 
                            _config.ConflictResolution, 
                            _config.PreserveTimestamps,
                            _config.VerificationMode);
                    }
                    catch
                    {
                        // Fall back to custom if TeraCopy fails to initialize
                        _config.CopyEngine = CopyEngine.CustomFast;
                    }
                }
                else
                {
                    // TeraCopy not found, fall back to custom
                    _config.CopyEngine = CopyEngine.CustomFast;
                }
            }
            else if (_config.CopyEngine == CopyEngine.FastCopy)
            {
                var fastCopyPath = EngineDetector.DetectFastCopy();
                if (fastCopyPath.IsInstalled)
                {
                    try
                    {
                        _fastCopyEngine = new FastCopyEngine(
                            fastCopyPath.InstallPath, 
                            _config.ConflictResolution, 
                            _config.PreserveTimestamps,
                            _config.VerificationMode);
                    }
                    catch
                    {
                        // Fall back to custom if FastCopy fails to initialize
                        _config.CopyEngine = CopyEngine.CustomFast;
                    }
                }
                else
                {
                    // FastCopy not found, fall back to custom
                    _config.CopyEngine = CopyEngine.CustomFast;
                }
            }
        }

        /// <summary>
        /// Processes a queue of files using the configured engine
        /// </summary>
        public async Task<OperationResult> ProcessQueueAsync(
            List<QueueEntry> queue,
            string destinationRoot,
            Action<string> statusCallback = null,
            IProgress<OperationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new OperationResult
            {
                TotalFiles = queue.Count,
                SuccessCount = 0,
                FailedCount = 0,
                SkippedCount = 0,
                TotalBytesProcessed = 0,
                StartTime = DateTime.Now,
                FilesVerified = 0,
                VerificationPassed = 0,
                VerificationFailed = 0,
                VerificationRetried = 0
            };

            var processedFiles = 0;

            foreach (var entry in queue)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Status = "Cancelled";
                    break;
                }

                try
                {
                    var destinationPath = BuildDestinationPath(entry, destinationRoot);

                    // Ensure destination directory exists
                    var destDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    // Check for conflicts BEFORE operation
                    if (File.Exists(destinationPath))
                    {
                        var shouldSkip = await HandleConflictAsync(entry.SourcePath, destinationPath);
                        if (shouldSkip)
                        {
                            entry.Status = "Skipped (exists)";
                            result.SkippedCount++;
                            processedFiles++;
                            
                            progress?.Report(new OperationProgress
                            {
                                ProcessedFiles = processedFiles,
                                TotalFiles = queue.Count,
                                CurrentFile = entry.FileName,
                                PercentComplete = (double)processedFiles / queue.Count * 100
                            });
                            
                            continue;
                        }
                    }

                    // Route to appropriate engine
                    var copyResult = await ExecuteFileOperationAsync(entry, destinationPath, statusCallback, progress, cancellationToken);

                    if (copyResult.Success)
                    {
                        entry.Status = _config.OperationMode == FileOperationMode.Move ? "Moved" : "Copied";
                        entry.DestinationPath = destinationPath;
                        result.SuccessCount++;
                        result.TotalBytesProcessed += entry.SizeBytes;
                        
                        // Track verification data
                        if (copyResult.Verified || copyResult.VerificationMode != VerificationMode.None)
                        {
                            result.FilesVerified++;
                            
                            if (copyResult.Verified)
                                result.VerificationPassed++;
                            else if (copyResult.VerificationFailed)
                                result.VerificationFailed++;
                                
                            if (copyResult.VerificationRetries > 0)
                                result.VerificationRetried++;
                            
                            // Update queue entry verification status
                            entry.Verified = copyResult.Verified;
                            entry.VerificationRetries = copyResult.VerificationRetries;
                            entry.VerificationFailed = copyResult.VerificationFailed;
                            
                            if (copyResult.VerificationMode == VerificationMode.None)
                                entry.VerificationMethod = "None";
                            else if (!string.IsNullOrEmpty(copyResult.SourceHash))
                                entry.VerificationMethod = "✅ SHA256";
                            else if (copyResult.Verified)
                                entry.VerificationMethod = "✅ Size";
                            else
                                entry.VerificationMethod = "❌ Failed";
                            
                            if (copyResult.VerificationRetries > 0 && copyResult.Verified)
                                entry.VerificationMethod += $" (Retry {copyResult.VerificationRetries})";
                        }
                    }
                    else
                    {
                        entry.Status = "Failed";
                        result.FailedCount++;
                        
                        // Check ContinueOnErrors setting
                        if (!_config.ContinueOnErrors)
                        {
                            statusCallback?.Invoke("Operation stopped due to file operation failure");
                            result.Status = "Stopped on error";
                            break; // Exit loop - stop processing
                        }
                    }
                }
                catch (Exception ex)
                {
                    entry.Status = $"Failed: {ex.Message}";
                    result.FailedCount++;
                    
                    // Check ContinueOnErrors setting
                    if (!_config.ContinueOnErrors)
                    {
                        statusCallback?.Invoke($"Operation stopped: {ex.Message}");
                        result.Status = "Stopped on error";
                        break; // Exit loop - stop processing
                    }
                }

                processedFiles++;
                progress?.Report(new OperationProgress
                {
                    ProcessedFiles = processedFiles,
                    TotalFiles = queue.Count,
                    CurrentFile = entry.FileName,
                    PercentComplete = (double)processedFiles / queue.Count * 100
                });
            }

            result.EndTime = DateTime.Now;
            result.Status = result.FailedCount == 0 ? "Completed" : "Completed with errors";
            return result;
        }

        /// <summary>
        /// Execute file operation using the configured engine
        /// </summary>
        private async Task<CopyResult> ExecuteFileOperationAsync(
            QueueEntry entry,
            string destinationPath,
            Action<string> statusCallback,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                bool isMove = _config.OperationMode == FileOperationMode.Move;

                switch (_config.CopyEngine)
                {
                    case CopyEngine.CustomFast:
                        return await ExecuteCustomFastAsync(entry.SourcePath, destinationPath, isMove, statusCallback, cancellationToken);

                    case CopyEngine.TeraCopy:
                        if (_teraCopyEngine != null)
                        {
                            var tcSuccess = await ExecuteTeraCopyAsync(entry.SourcePath, destinationPath, isMove, cancellationToken);
                            return new CopyResult 
                            { 
                                Success = tcSuccess,
                                SourcePath = entry.SourcePath,
                                DestinationPath = destinationPath,
                                TotalBytes = entry.SizeBytes,
                                Verified = _config.VerificationMode != VerificationMode.None,
                                VerificationMode = _config.VerificationMode
                            };
                        }
                        else
                            return await ExecuteCustomFastAsync(entry.SourcePath, destinationPath, isMove, statusCallback, cancellationToken);

                    case CopyEngine.FastCopy:
                        if (_fastCopyEngine != null)
                        {
                            var fcSuccess = await ExecuteFastCopyAsync(entry.SourcePath, destinationPath, isMove, cancellationToken);
                            return new CopyResult 
                            { 
                                Success = fcSuccess,
                                SourcePath = entry.SourcePath,
                                DestinationPath = destinationPath,
                                TotalBytes = entry.SizeBytes,
                                Verified = _config.VerificationMode != VerificationMode.None,
                                VerificationMode = _config.VerificationMode
                            };
                        }
                        else
                            return await ExecuteCustomFastAsync(entry.SourcePath, destinationPath, isMove, statusCallback, cancellationToken);

                    case CopyEngine.WindowsStandard:
                    default:
                        var wsSuccess = await ExecuteWindowsStandardAsync(entry.SourcePath, destinationPath, isMove, cancellationToken);
                        return new CopyResult 
                        { 
                            Success = wsSuccess,
                            SourcePath = entry.SourcePath,
                            DestinationPath = destinationPath,
                            TotalBytes = entry.SizeBytes,
                            Verified = false,
                            VerificationMode = VerificationMode.None
                        };
                }
            }
            catch
            {
                return new CopyResult 
                { 
                    Success = false,
                    SourcePath = entry.SourcePath,
                    DestinationPath = destinationPath,
                    ErrorMessage = "Operation failed"
                };
            }
        }

        /// <summary>
        /// Execute using Custom Fast Engine
        /// </summary>
        private async Task<CopyResult> ExecuteCustomFastAsync(
            string sourcePath,
            string destinationPath,
            bool isMove,
            Action<string> statusCallback,
            CancellationToken cancellationToken)
        {
            CopyResult result;

            if (isMove)
            {
                result = await _customEngine.MoveFileAsync(
                    sourcePath, 
                    destinationPath, 
                    _config.PreserveTimestamps,
                    _config.VerificationMode,
                    _config.RetryAttempts,
                    _config.RetryDelaySeconds,
                    statusCallback,
                    null, 
                    cancellationToken);
            }
            else
            {
                result = await _customEngine.CopyFileAsync(
                    sourcePath, 
                    destinationPath, 
                    _config.PreserveTimestamps,
                    _config.VerificationMode,
                    _config.RetryAttempts,
                    _config.RetryDelaySeconds,
                    statusCallback,
                    null, 
                    cancellationToken);
            }

            return result;
        }

        /// <summary>
        /// Execute using TeraCopy
        /// </summary>
        private async Task<bool> ExecuteTeraCopyAsync(
            string sourcePath,
            string destinationPath,
            bool isMove,
            CancellationToken cancellationToken)
        {
            TeraCopyResult result;

            if (isMove)
            {
                result = await _teraCopyEngine.MoveFileAsync(sourcePath, destinationPath, null, cancellationToken);
            }
            else
            {
                result = await _teraCopyEngine.CopyFileAsync(sourcePath, destinationPath, null, cancellationToken);
            }

            return result.Success;
        }

        /// <summary>
        /// Execute using FastCopy
        /// </summary>
        private async Task<bool> ExecuteFastCopyAsync(
            string sourcePath,
            string destinationPath,
            bool isMove,
            CancellationToken cancellationToken)
        {
            FastCopyResult result;

            if (isMove)
            {
                result = await _fastCopyEngine.MoveFileAsync(sourcePath, destinationPath, null, cancellationToken);
            }
            else
            {
                result = await _fastCopyEngine.CopyFileAsync(sourcePath, destinationPath, null, cancellationToken);
            }

            return result.Success;
        }

        /// <summary>
        /// Execute using Windows Standard (File.Copy/File.Move)
        /// </summary>
        private async Task<bool> ExecuteWindowsStandardAsync(
            string sourcePath,
            string destinationPath,
            bool isMove,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                if (isMove)
                {
                    File.Move(sourcePath, destinationPath, true);
                }
                else
                {
                    File.Copy(sourcePath, destinationPath, true);
                }
            }, cancellationToken);

            return true;
        }

        /// <summary>
        /// Handle file conflicts based on resolution strategy
        /// </summary>
        private async Task<bool> HandleConflictAsync(string sourcePath, string destinationPath)
        {
            switch (_config.ConflictResolution)
            {
                case FileConflictResolution.Skip:
                    return true; // Skip this file

                case FileConflictResolution.OverwriteIfNewer:
                    var sourceTime = File.GetLastWriteTime(sourcePath);
                    var destTime = File.GetLastWriteTime(destinationPath);
                    if (destTime >= sourceTime)
                    {
                        return true; // Skip - destination is newer or same
                    }
                    break;

                case FileConflictResolution.RenameKeepBoth:
                    // This is handled by the engines themselves
                    // For Windows Standard, we need to handle it here
                    if (_config.CopyEngine == CopyEngine.WindowsStandard)
                    {
                        // Rename not implemented for Windows Standard
                        // Fall through to overwrite
                    }
                    break;

                case FileConflictResolution.Overwrite:
                default:
                    // Proceed with overwrite
                    break;
            }

            return false; // Don't skip - proceed with operation
        }

        /// <summary>
        /// Gets the date-based folder path based on file's modification date
        /// </summary>
        private string GetDateFolder(string filePath, string dateFormat)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var modifiedDate = fileInfo.LastWriteTime;
                
                // Parse date format template and generate folder path
                return dateFormat switch
                {
                    "Year\\Month (2024\\02)" => 
                        Path.Combine(modifiedDate.Year.ToString(), modifiedDate.Month.ToString("D2")),
                    
                    "Year (2024)" => 
                        modifiedDate.Year.ToString(),
                    
                    "Year-Month (2024-02)" => 
                        $"{modifiedDate.Year}-{modifiedDate.Month:D2}",
                    
                    "Month\\Year (02\\2024)" => 
                        Path.Combine(modifiedDate.Month.ToString("D2"), modifiedDate.Year.ToString()),
                    
                    _ => modifiedDate.Year.ToString() // Default to year
                };
            }
            catch
            {
                return ""; // Fall back to no date folder on error
            }
        }

        /// <summary>
        /// Builds the destination path based on structure mode
        /// </summary>
        private string BuildDestinationPath(QueueEntry entry, string destinationRoot)
        {
            var fileName = Path.GetFileName(entry.SourcePath);
            
            // Get date folder if date organization is enabled
            string dateFolder = "";
            if (_config.EnableDateOrganization && !string.IsNullOrEmpty(_config.DateFormat))
            {
                dateFolder = GetDateFolder(entry.SourcePath, _config.DateFormat);
            }
            
            // Handle Semi-Excluded files (flatten structure, organize by category only)
            if (entry.IsSemiExcluded)
            {
                // Semi-excluded files are ALWAYS organized by category only (structure is flattened)
                if (!string.IsNullOrEmpty(dateFolder))
                {
                    // Date\Category\file
                    return Path.Combine(destinationRoot, dateFolder, entry.Category, fileName);
                }
                else
                {
                    // Category\file
                    return Path.Combine(destinationRoot, entry.Category, fileName);
                }
            }
            
            // Handle normal files based on structure mode
            switch (_config.StructureMode)
            {
                case DestinationStructureMode.OrganizeByCategory:
                    // Organize by category only
                    if (!string.IsNullOrEmpty(dateFolder))
                    {
                        // Date\Category\file
                        return Path.Combine(destinationRoot, dateFolder, entry.Category, fileName);
                    }
                    else
                    {
                        // Category\file
                        return Path.Combine(destinationRoot, entry.Category, fileName);
                    }

                case DestinationStructureMode.PreserveStructure:
                    // Preserve the exact folder structure from source
                    var sourceDir = Path.GetDirectoryName(entry.SourcePath);
                    var sourceRoot = _config.SourceFolder;
                    
                    if (!string.IsNullOrEmpty(sourceDir) && !string.IsNullOrEmpty(sourceRoot))
                    {
                        var relativePath = Path.GetRelativePath(sourceRoot, sourceDir);
                        if (relativePath != ".")
                        {
                            if (!string.IsNullOrEmpty(dateFolder))
                            {
                                // Date\PreservedStructure\file
                                return Path.Combine(destinationRoot, dateFolder, relativePath, fileName);
                            }
                            else
                            {
                                // PreservedStructure\file
                                return Path.Combine(destinationRoot, relativePath, fileName);
                            }
                        }
                    }
                    
                    // File is in source root
                    if (!string.IsNullOrEmpty(dateFolder))
                    {
                        // Date\file
                        return Path.Combine(destinationRoot, dateFolder, fileName);
                    }
                    else
                    {
                        // file
                        return Path.Combine(destinationRoot, fileName);
                    }

                case DestinationStructureMode.Hybrid:
                    // Category first, then preserve subfolder structure
                    var srcDir = Path.GetDirectoryName(entry.SourcePath);
                    var srcRoot = _config.SourceFolder;
                    
                    if (!string.IsNullOrEmpty(srcDir) && !string.IsNullOrEmpty(srcRoot))
                    {
                        var relPath = Path.GetRelativePath(srcRoot, srcDir);
                        if (relPath != ".")
                        {
                            if (!string.IsNullOrEmpty(dateFolder))
                            {
                                // Date\Category\PreservedStructure\file
                                return Path.Combine(destinationRoot, dateFolder, entry.Category, relPath, fileName);
                            }
                            else
                            {
                                // Category\PreservedStructure\file
                                return Path.Combine(destinationRoot, entry.Category, relPath, fileName);
                            }
                        }
                    }
                    
                    // File is in source root
                    if (!string.IsNullOrEmpty(dateFolder))
                    {
                        // Date\Category\file
                        return Path.Combine(destinationRoot, dateFolder, entry.Category, fileName);
                    }
                    else
                    {
                        // Category\file
                        return Path.Combine(destinationRoot, entry.Category, fileName);
                    }

                default:
                    // Fallback
                    if (!string.IsNullOrEmpty(dateFolder))
                    {
                        return Path.Combine(destinationRoot, dateFolder, fileName);
                    }
                    else
                    {
                        return Path.Combine(destinationRoot, fileName);
                    }
            }
        }
    }

    /// <summary>
    /// Progress information for file operations
    /// </summary>
    public class OperationProgress
    {
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFile { get; set; }
        public double PercentComplete { get; set; }
    }

    /// <summary>
    /// Result of a file operation
    /// </summary>
    public class OperationResult
    {
        public int TotalFiles { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public long TotalBytesProcessed { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; }
        
        // Verification tracking
        public int FilesVerified { get; set; }
        public int VerificationPassed { get; set; }
        public int VerificationFailed { get; set; }
        public int VerificationRetried { get; set; }

        public TimeSpan Duration => EndTime - StartTime;
        public double SuccessRate => TotalFiles > 0 ? (double)SuccessCount / TotalFiles * 100 : 0;
        public double VerificationSuccessRate => FilesVerified > 0 ? (double)VerificationPassed / FilesVerified * 100 : 0;
    }
}
