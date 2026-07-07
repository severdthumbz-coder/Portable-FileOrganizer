using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Custom high-performance file copy engine with multi-threading and optimization
    /// </summary>
    public class CustomFastCopyEngine
    {
        private const int BufferSize = 8 * 1024 * 1024; // 8MB buffer (vs .NET default 4KB)
        private const int MaxConcurrentOperations = 4; // Number of parallel file operations
        private const long SmartModeThreshold = 10 * 1024 * 1024; // 10MB threshold for smart mode
        private SemaphoreSlim _semaphore;

        public CustomFastCopyEngine()
        {
            _semaphore = new SemaphoreSlim(MaxConcurrentOperations, MaxConcurrentOperations);
        }

        /// <summary>
        /// Copies a file with optimized buffering and progress reporting
        /// </summary>
        public async Task<CopyResult> CopyFileAsync(
            string sourcePath,
            string destinationPath,
            bool preserveTimestamps = true,
            VerificationMode verificationMode = VerificationMode.Smart,
            int retryAttempts = 3,
            int retryDelaySeconds = 2,
            Action<string> statusCallback = null,
            IProgress<FileProgress> progress = null,
            CancellationToken cancellationToken = default,
            int currentRetry = 0)
        {
            var result = new CopyResult
            {
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                Success = false
            };

            try
            {
                // Wait for semaphore slot (limits concurrent operations)
                await _semaphore.WaitAsync(cancellationToken);

                try
                {
                    var fileInfo = new FileInfo(sourcePath);
                    result.TotalBytes = fileInfo.Length;
                    var startTime = DateTime.Now;

                    // Ensure destination directory exists
                    var destDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    // Optimized file copy with progress
                    await CopyFileWithProgressAsync(
                        sourcePath, 
                        destinationPath, 
                        fileInfo.Length,
                        progress, 
                        cancellationToken);

                    // Preserve file attributes and timestamps (if requested)
                    if (preserveTimestamps)
                    {
                        File.SetAttributes(destinationPath, File.GetAttributes(sourcePath));
                        File.SetCreationTime(destinationPath, File.GetCreationTime(sourcePath));
                        File.SetLastWriteTime(destinationPath, File.GetLastWriteTime(sourcePath));
                        File.SetLastAccessTime(destinationPath, File.GetLastAccessTime(sourcePath));
                    }

                    // DATA INTEGRITY VERIFICATION
                    var verificationResult = await VerifyFileIntegrityAsync(
                        sourcePath, 
                        destinationPath, 
                        verificationMode, 
                        fileInfo.Length,
                        statusCallback);

                    if (!verificationResult.Passed)
                    {
                        // Verification failed - retry if attempts remaining
                        if (currentRetry < retryAttempts)
                        {
                            _semaphore.Release(); // Release before retry
                            
                            // Delete corrupted/incomplete destination file
                            if (File.Exists(destinationPath))
                            {
                                File.Delete(destinationPath);
                            }
                            
                            // Notify retry
                            statusCallback?.Invoke($"Retrying verification: {Path.GetFileName(sourcePath)} (Attempt {currentRetry + 2}/{retryAttempts})");
                            
                            // Wait before retry
                            await Task.Delay(retryDelaySeconds * 1000, cancellationToken);
                            
                            // Retry
                            return await CopyFileAsync(
                                sourcePath, 
                                destinationPath, 
                                preserveTimestamps, 
                                verificationMode, 
                                retryAttempts, 
                                retryDelaySeconds, 
                                statusCallback,
                                progress, 
                                cancellationToken, 
                                currentRetry + 1);
                        }
                        
                        // No more retries - fail
                        result.Success = false;
                        result.Verified = false;
                        result.VerificationFailed = true;
                        result.VerificationRetries = currentRetry;
                        result.SourceHash = verificationResult.SourceHash;
                        result.DestHash = verificationResult.DestHash;
                        result.ErrorMessage = $"Verification failed after {retryAttempts} attempts: {verificationResult.FailureReason}";
                        return result;
                    }

                    result.BytesCopied = fileInfo.Length;
                    result.Success = true;
                    result.Verified = true;
                    result.VerificationMode = verificationMode;
                    result.VerificationRetries = currentRetry;
                    result.SourceHash = verificationResult.SourceHash;
                    result.DestHash = verificationResult.DestHash;
                    result.Duration = DateTime.Now - startTime;
                    result.SpeedMBps = result.TotalBytes / (1024.0 * 1024.0) / result.Duration.TotalSeconds;
                }
                finally
                {
                    _semaphore.Release();
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
        /// Moves a file (optimized copy + delete source)
        /// </summary>
        public async Task<CopyResult> MoveFileAsync(
            string sourcePath,
            string destinationPath,
            bool preserveTimestamps = true,
            VerificationMode verificationMode = VerificationMode.Smart,
            int retryAttempts = 3,
            int retryDelaySeconds = 2,
            Action<string> statusCallback = null,
            IProgress<FileProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            // Try native move first (instant if same volume)
            try
            {
                var sourceRoot = Path.GetPathRoot(sourcePath);
                var destRoot = Path.GetPathRoot(destinationPath);

                // If same drive, use native move (instant)
                if (string.Equals(sourceRoot, destRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var destDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Move(sourcePath, destinationPath, true);
                    // Note: File.Move automatically preserves all timestamps

                    var fileInfo = new FileInfo(destinationPath);
                    return new CopyResult
                    {
                        SourcePath = sourcePath,
                        DestinationPath = destinationPath,
                        Success = true,
                        TotalBytes = fileInfo.Length,
                        BytesCopied = fileInfo.Length,
                        Duration = TimeSpan.FromMilliseconds(1),
                        SpeedMBps = 0 // Instant move
                    };
                }
            }
            catch
            {
                // Fall through to copy + delete
            }

            // Different drives - use optimized copy + delete
            var result = await CopyFileAsync(
                sourcePath, 
                destinationPath, 
                preserveTimestamps, 
                verificationMode, 
                retryAttempts, 
                retryDelaySeconds, 
                statusCallback,
                progress, 
                cancellationToken);

            if (result.Success)
            {
                try
                {
                    File.Delete(sourcePath);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"File copied but failed to delete source: {ex.Message}";
                }
            }

            return result;
        }

        /// <summary>
        /// Core optimized file copy with progress reporting
        /// </summary>
        private async Task CopyFileWithProgressAsync(
            string sourcePath,
            string destinationPath,
            long totalBytes,
            IProgress<FileProgress> progress,
            CancellationToken cancellationToken)
        {
            using (var sourceStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var destinationStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                byte[] buffer = new byte[BufferSize];
                long totalBytesRead = 0;
                int bytesRead;
                var lastProgressReport = DateTime.Now;

                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await destinationStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalBytesRead += bytesRead;

                    // Report progress (throttled to every 100ms to avoid UI spam)
                    if (progress != null && (DateTime.Now - lastProgressReport).TotalMilliseconds >= 100)
                    {
                        progress.Report(new FileProgress
                        {
                            FileName = Path.GetFileName(sourcePath),
                            BytesProcessed = totalBytesRead,
                            TotalBytes = totalBytes,
                            PercentComplete = (double)totalBytesRead / totalBytes * 100
                        });
                        lastProgressReport = DateTime.Now;
                    }
                }

                // Final progress report
                progress?.Report(new FileProgress
                {
                    FileName = Path.GetFileName(sourcePath),
                    BytesProcessed = totalBytes,
                    TotalBytes = totalBytes,
                    PercentComplete = 100
                });
            }
        }

        /// <summary>
        /// Result of file verification
        /// </summary>
        private class VerificationResult
        {
            public bool Passed { get; set; }
            public string SourceHash { get; set; }
            public string DestHash { get; set; }
            public string FailureReason { get; set; }
        }

        /// <summary>
        /// Verifies file integrity based on selected verification mode
        /// </summary>
        private async Task<VerificationResult> VerifyFileIntegrityAsync(
            string sourcePath,
            string destinationPath,
            VerificationMode mode,
            long fileSize,
            Action<string> statusCallback = null)
        {
            var result = new VerificationResult { Passed = false };
            
            try
            {
                var fileName = Path.GetFileName(destinationPath);
                
                // Report verification start
                if (mode != VerificationMode.None)
                {
                    var methodDesc = mode == VerificationMode.FullHash || (mode == VerificationMode.Smart && fileSize < SmartModeThreshold)
                        ? "SHA256"
                        : "Size";
                    statusCallback?.Invoke($"Verifying: {fileName} ({methodDesc})");
                }
                
                // Size verification (always performed, instant)
                var destInfo = new FileInfo(destinationPath);
                if (destInfo.Length != fileSize)
                {
                    result.FailureReason = $"Size mismatch: expected {fileSize} bytes, got {destInfo.Length} bytes";
                    return result; // Size mismatch
                }

                // Additional verification based on mode
                switch (mode)
                {
                    case VerificationMode.None:
                        result.Passed = true;
                        return result; // No verification

                    case VerificationMode.SizeOnly:
                        result.Passed = true;
                        return result; // Size already verified above

                    case VerificationMode.Smart:
                        // Hash files under 10MB, size check for larger
                        if (fileSize < SmartModeThreshold)
                        {
                            var hashResult = await VerifyHashAsync(sourcePath, destinationPath);
                            if (!hashResult.Passed)
                            {
                                result.SourceHash = hashResult.SourceHash;
                                result.DestHash = hashResult.DestHash;
                                result.FailureReason = "Hash mismatch";
                                return result;
                            }
                            result.SourceHash = hashResult.SourceHash;
                            result.DestHash = hashResult.DestHash;
                            result.Passed = true;
                            return result;
                        }
                        result.Passed = true;
                        return result; // Size check sufficient for large files

                    case VerificationMode.FullHash:
                        // Always hash
                        var fullHashResult = await VerifyHashAsync(sourcePath, destinationPath);
                        if (!fullHashResult.Passed)
                        {
                            result.SourceHash = fullHashResult.SourceHash;
                            result.DestHash = fullHashResult.DestHash;
                            result.FailureReason = "Hash mismatch";
                            return result;
                        }
                        result.SourceHash = fullHashResult.SourceHash;
                        result.DestHash = fullHashResult.DestHash;
                        result.Passed = true;
                        return result;

                    default:
                        result.Passed = true;
                        return result;
                }
            }
            catch (Exception ex)
            {
                result.FailureReason = $"Verification error: {ex.Message}";
                return result; // Verification error = fail
            }
        }

        /// <summary>
        /// Computes and compares SHA256 hashes of source and destination files
        /// </summary>
        private async Task<VerificationResult> VerifyHashAsync(string sourcePath, string destinationPath)
        {
            var result = new VerificationResult();
            try
            {
                result.SourceHash = await ComputeFileHashAsync(sourcePath);
                result.DestHash = await ComputeFileHashAsync(destinationPath);
                result.Passed = result.SourceHash == result.DestHash;
                return result;
            }
            catch (Exception ex)
            {
                result.FailureReason = $"Hash computation failed: {ex.Message}";
                result.Passed = false;
                return result;
            }
        }

        /// <summary>
        /// Computes SHA256 hash of a file
        /// </summary>
        private async Task<string> ComputeFileHashAsync(string path)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var hashBytes = await sha256.ComputeHashAsync(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        /// <summary>
        /// Batch copy multiple files with optimal parallelism
        /// </summary>
        public async Task<BatchCopyResult> CopyBatchAsync(
            string[] sourcePaths,
            string[] destinationPaths,
            IProgress<BatchProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (sourcePaths.Length != destinationPaths.Length)
                throw new ArgumentException("Source and destination arrays must have same length");

            var result = new BatchCopyResult
            {
                TotalFiles = sourcePaths.Length,
                StartTime = DateTime.Now
            };

            var tasks = sourcePaths.Select(async (source, index) =>
            {
                var destination = destinationPaths[index];
                var fileResult = await CopyFileAsync(
                    source, 
                    destination, 
                    true, // preserveTimestamps
                    VerificationMode.Smart, // verificationMode
                    3, // retryAttempts
                    2, // retryDelaySeconds
                    null, // statusCallback
                    null, // progress
                    cancellationToken);

                lock (result)
                {
                    if (fileResult.Success)
                    {
                        result.SuccessCount++;
                        result.TotalBytesCopied += fileResult.BytesCopied;
                    }
                    else
                    {
                        result.FailedCount++;
                    }

                    result.ProcessedFiles++;

                    progress?.Report(new BatchProgress
                    {
                        ProcessedFiles = result.ProcessedFiles,
                        TotalFiles = result.TotalFiles,
                        SuccessCount = result.SuccessCount,
                        FailedCount = result.FailedCount,
                        CurrentFile = Path.GetFileName(source),
                        PercentComplete = (double)result.ProcessedFiles / result.TotalFiles * 100
                    });
                }

                return fileResult;
            });

            await Task.WhenAll(tasks);

            result.EndTime = DateTime.Now;
            result.Duration = result.EndTime - result.StartTime;

            return result;
        }
    }

    /// <summary>
    /// Progress information for a single file copy
    /// </summary>
    public class FileProgress
    {
        public string FileName { get; set; }
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public double PercentComplete { get; set; }
    }

    /// <summary>
    /// Result of a single file copy operation
    /// </summary>
    public class CopyResult
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public bool Success { get; set; }
        public long TotalBytes { get; set; }
        public long BytesCopied { get; set; }
        public TimeSpan Duration { get; set; }
        public double SpeedMBps { get; set; }
        public string ErrorMessage { get; set; }
        
        // Verification tracking
        public bool Verified { get; set; } = false;
        public VerificationMode VerificationMode { get; set; } = VerificationMode.None;
        public int VerificationRetries { get; set; } = 0;
        public bool VerificationFailed { get; set; } = false;
        public string SourceHash { get; set; }
        public string DestHash { get; set; }
    }

    /// <summary>
    /// Progress for batch operations
    /// </summary>
    public class BatchProgress
    {
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public string CurrentFile { get; set; }
        public double PercentComplete { get; set; }
    }

    /// <summary>
    /// Result of batch copy operation
    /// </summary>
    public class BatchCopyResult
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public long TotalBytesCopied { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }

        public double AverageSpeedMBps => TotalBytesCopied / (1024.0 * 1024.0) / Duration.TotalSeconds;
    }
}
