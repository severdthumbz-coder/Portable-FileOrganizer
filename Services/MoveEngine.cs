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
                            _config.VerifyExternalCopies ? _config.VerificationMode : VerificationMode.None);
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
                            _config.VerifyExternalCopies ? _config.VerificationMode : VerificationMode.None);
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
        /// Organizes a single file to an explicit destination path, applying the given
        /// operation (move/copy) and conflict resolution. Used by the automation triggers
        /// (folder watcher, scheduler) after the RuleEngine has decided the destination.
        /// Uses the safe CustomFast engine (copy-verify, and for a move only deletes the
        /// source after a successful copy). Returns the CopyResult.
        /// </summary>
        public async Task<CopyResult> OrganizeFileAsync(
            string sourcePath,
            string destinationPath,
            bool isMove,
            FileConflictResolution conflict,
            CancellationToken cancellationToken = default)
        {
            var result = new CopyResult
            {
                SourcePath = sourcePath,
                DestinationPath = destinationPath
            };

            try
            {
                if (!File.Exists(sourcePath))
                {
                    result.Success = false;
                    result.ErrorMessage = "Source file no longer exists.";
                    return result;
                }

                var destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                // Conflict handling for an existing destination.
                if (File.Exists(destinationPath))
                {
                    switch (conflict)
                    {
                        case FileConflictResolution.Skip:
                            result.Success = true; // treat as a no-op success
                            result.ErrorMessage = "Skipped (destination already exists).";
                            return result;

                        case FileConflictResolution.OverwriteIfNewer:
                            if (File.GetLastWriteTime(destinationPath) >= File.GetLastWriteTime(sourcePath))
                            {
                                result.Success = true;
                                result.ErrorMessage = "Skipped (destination is newer or same).";
                                return result;
                            }
                            break;

                        case FileConflictResolution.RenameKeepBoth:
                            destinationPath = GetUniqueDestination(destinationPath);
                            result.DestinationPath = destinationPath;
                            break;

                        case FileConflictResolution.Overwrite:
                        default:
                            break; // engines overwrite
                    }
                }

                if (isMove)
                {
                    result = await _customEngine.MoveFileAsync(
                        sourcePath, destinationPath,
                        _config.PreserveTimestamps, _config.VerificationMode,
                        _config.RetryAttempts, _config.RetryDelaySeconds,
                        null, null, cancellationToken);
                }
                else
                {
                    result = await _customEngine.CopyFileAsync(
                        sourcePath, destinationPath,
                        _config.PreserveTimestamps, _config.VerificationMode,
                        _config.RetryAttempts, _config.RetryDelaySeconds,
                        null, null, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Appends " (n)" before the extension until a non-existing path is found.
        /// </summary>
        private string GetUniqueDestination(string destinationPath)
        {
            var dir = Path.GetDirectoryName(destinationPath);
            var name = Path.GetFileNameWithoutExtension(destinationPath);
            var ext = Path.GetExtension(destinationPath);
            int n = 1;
            string candidate = destinationPath;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(dir, $"{name} ({n}){ext}");
                n++;
            }
            return candidate;
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
                            // Always run the external tool in COPY mode (isMove: false) so a failed
                            // verification never leaves us with a deleted source. The finalizer
                            // handles rename, verification, and (for a move) source deletion.
                            var tcSuccess = await ExecuteTeraCopyAsync(entry.SourcePath, destinationPath, false, cancellationToken);
                            return await FinalizeExternalCopyAsync(entry.SourcePath, destinationPath, isMove, tcSuccess, entry.SizeBytes);
                        }
                        else
                            return await ExecuteCustomFastAsync(entry.SourcePath, destinationPath, isMove, statusCallback, cancellationToken);

                    case CopyEngine.FastCopy:
                        if (_fastCopyEngine != null)
                        {
                            var fcSuccess = await ExecuteFastCopyAsync(entry.SourcePath, destinationPath, false, cancellationToken);
                            return await FinalizeExternalCopyAsync(entry.SourcePath, destinationPath, isMove, fcSuccess, entry.SizeBytes);
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
        /// Finalizes an external-engine (TeraCopy/FastCopy) copy: applies any required
        /// rename, verifies the result when enabled, and — for a move — deletes the
        /// source ONLY after verification passes. The external tool is always run in
        /// copy mode (never move) so a failed verification never destroys the source.
        /// </summary>
        private async Task<CopyResult> FinalizeExternalCopyAsync(
            string sourcePath,
            string destinationPath,
            bool isMove,
            bool toolReportedSuccess,
            long totalBytes)
        {
            var result = new CopyResult
            {
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                TotalBytes = totalBytes,
                VerificationMode = _config.VerificationMode
            };

            if (!toolReportedSuccess)
            {
                result.Success = false;
                result.ErrorMessage = "External copy tool reported failure.";
                return result;
            }

            // External tools copy into the destination FOLDER using the ORIGINAL filename.
            // If the intended destination filename differs (a rename), move it into place.
            var destFolder = Path.GetDirectoryName(destinationPath);
            var landedPath = Path.Combine(destFolder, Path.GetFileName(sourcePath));

            try
            {
                if (!string.Equals(landedPath, destinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(landedPath))
                    {
                        File.Move(landedPath, destinationPath, true); // rename into final name
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Copy succeeded but rename to final name failed: {ex.Message}";
                return result;
            }

            // Verification (only when enabled)
            if (_config.VerifyExternalCopies)
            {
                // 1) Cheap sanity check: destination exists and size matches source.
                try
                {
                    if (!File.Exists(destinationPath))
                    {
                        result.Success = false;
                        result.ErrorMessage = "Verification failed: destination file not found after copy.";
                        return result;
                    }

                    var srcInfo = new FileInfo(sourcePath);
                    var dstInfo = new FileInfo(destinationPath);
                    if (srcInfo.Exists && srcInfo.Length != dstInfo.Length)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Verification failed: size mismatch (source {srcInfo.Length}, destination {dstInfo.Length}).";
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Verification error: {ex.Message}";
                    return result;
                }

                // 2) Independent SHA-256 comparison when the user asked for FullHash.
                //    (Smart/SizeOnly rely on the size check above plus the tool's own verify.)
                if (_config.VerificationMode == VerificationMode.FullHash)
                {
                    try
                    {
                        var srcHash = await ComputeFileHashAsync(sourcePath);
                        var dstHash = await ComputeFileHashAsync(destinationPath);
                        if (!string.Equals(srcHash, dstHash, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Success = false;
                            result.ErrorMessage = "Verification failed: SHA-256 hash mismatch.";
                            return result;
                        }
                        result.SourceHash = srcHash;
                        result.DestHash = dstHash;
                    }
                    catch (Exception ex)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Hash verification error: {ex.Message}";
                        return result;
                    }
                }

                result.Verified = true;
            }
            else
            {
                result.Verified = false;
            }

            // For a move: delete the source ONLY now that copy (and any verification) succeeded.
            if (isMove)
            {
                try
                {
                    File.Delete(sourcePath);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"File copied/verified but failed to delete source: {ex.Message}";
                    return result;
                }
            }

            result.Success = true;
            result.BytesCopied = totalBytes;
            return result;
        }

        /// <summary>
        /// Computes the SHA-256 hash of a file (used for independent external-copy verification).
        /// </summary>
        private async Task<string> ComputeFileHashAsync(string path)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8 * 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var hashBytes = await sha256.ComputeHashAsync(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
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
