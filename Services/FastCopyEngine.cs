using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Integration with FastCopy for ultra high-performance file operations
    /// </summary>
    public class FastCopyEngine
    {
        private readonly string _fastCopyPath;
        private readonly FileConflictResolution _conflictResolution;
        private readonly bool _preserveTimestamps;
        private readonly VerificationMode _verificationMode;

        public FastCopyEngine(
            string fastCopyPath, 
            FileConflictResolution conflictResolution, 
            bool preserveTimestamps = true,
            VerificationMode verificationMode = VerificationMode.Smart)
        {
            _fastCopyPath = fastCopyPath ?? throw new ArgumentNullException(nameof(fastCopyPath));
            _conflictResolution = conflictResolution;
            _preserveTimestamps = preserveTimestamps;
            _verificationMode = verificationMode;

            if (!File.Exists(_fastCopyPath))
            {
                throw new FileNotFoundException($"FastCopy not found at: {_fastCopyPath}");
            }
        }

        /// <summary>
        /// Copy a file using FastCopy
        /// </summary>
        public async Task<FastCopyResult> CopyFileAsync(
            string sourcePath,
            string destinationPath,
            IProgress<FastCopyProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteFastCopyOperation(
                "diff",  // diff mode = copy with verification
                sourcePath,
                destinationPath,
                progress,
                cancellationToken);
        }

        /// <summary>
        /// Move a file using FastCopy
        /// </summary>
        public async Task<FastCopyResult> MoveFileAsync(
            string sourcePath,
            string destinationPath,
            IProgress<FastCopyProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteFastCopyOperation(
                "move",  // move mode
                sourcePath,
                destinationPath,
                progress,
                cancellationToken);
        }

        /// <summary>
        /// Execute FastCopy operation via command line
        /// </summary>
        private async Task<FastCopyResult> ExecuteFastCopyOperation(
            string mode,
            string sourcePath,
            string destinationPath,
            IProgress<FastCopyProgress> progress,
            CancellationToken cancellationToken)
        {
            var result = new FastCopyResult
            {
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                Success = false,
                StartTime = DateTime.Now
            };

            try
            {
                // Build FastCopy command line arguments
                var arguments = BuildCommandLineArguments(mode, sourcePath, destinationPath);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _fastCopyPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();

                    // Capture output for progress parsing
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            ParseProgressFromOutput(e.Data, progress);
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errorBuilder.AppendLine(e.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Wait for completion with cancellation support
                    while (!process.HasExited)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch { }
                            
                            result.Success = false;
                            result.ErrorMessage = "Operation cancelled";
                            return result;
                        }

                        await Task.Delay(100, cancellationToken);
                    }

                    result.EndTime = DateTime.Now;
                    result.Duration = result.EndTime - result.StartTime;
                    result.ExitCode = process.ExitCode;
                    result.Output = outputBuilder.ToString();
                    result.ErrorOutput = errorBuilder.ToString();

                    // FastCopy exit codes: 0 = success, non-zero = error
                    result.Success = process.ExitCode == 0;

                    if (!result.Success)
                    {
                        result.ErrorMessage = !string.IsNullOrEmpty(result.ErrorOutput) 
                            ? result.ErrorOutput 
                            : $"FastCopy operation failed with exit code {result.ExitCode}";
                    }
                    else
                    {
                        // Parse statistics from output
                        ParseStatistics(result.Output, result);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"FastCopy execution error: {ex.Message}";
                result.EndTime = DateTime.Now;
                result.Duration = result.EndTime - result.StartTime;
            }

            return result;
        }

        /// <summary>
        /// Build command line arguments for FastCopy
        /// </summary>
        private string BuildCommandLineArguments(string mode, string sourcePath, string destinationPath)
        {
            var destFolder = Path.GetDirectoryName(destinationPath);
            
            // FastCopy command format:
            // FastCopy.exe /cmd=mode "source" /to="destination_folder\" [options]
            
            var args = new StringBuilder();
            args.Append($"/cmd={mode} ");
            // Source is a POSITIONAL argument. (/srcfile is for a text file listing sources,
            // not the file being copied — using it for the source path was a bug.)
            args.Append($"\"{sourcePath}\" ");
            args.Append($"/to=\"{destFolder}\\\" ");

            // Conflict resolution options
            switch (_conflictResolution)
            {
                case FileConflictResolution.Skip:
                    args.Append("/error_stop=FALSE ");  // Continue on errors
                    args.Append("/skip_empty_dir ");    // Skip if exists
                    break;
                case FileConflictResolution.Overwrite:
                    args.Append("/force_close ");       // Force overwrite
                    args.Append("/overwrite ");
                    break;
                case FileConflictResolution.OverwriteIfNewer:
                    args.Append("/update ");            // Update if newer
                    break;
                case FileConflictResolution.RenameKeepBoth:
                    // FastCopy doesn't have native rename, fall back to overwrite
                    args.Append("/force_close ");
                    break;
            }

            // Performance options
            args.Append("/speed=full ");           // Maximum speed
            args.Append("/bufsize=512 ");          // 512MB buffer
            args.Append("/io_max=128 ");           // Max I/O operations
            
            // Verification if enabled (not None mode)
            if (_verificationMode != VerificationMode.None)
            {
                args.Append("/verify ");           // Verify after copy
            }
            
            args.Append("/linkdest ");             // Follow links
            args.Append("/acl ");                  // Copy ACLs
            args.Append("/stream ");               // Copy alternate streams
            args.Append("/reparse ");              // Copy reparse points
            
            // Timestamp preservation
            if (_preserveTimestamps)
            {
                args.Append("/timestamp ");        // Preserve all timestamps
            }
            
            // Operational options
            args.Append("/auto_close ");           // Auto close when done
            args.Append("/no_ui ");                // No GUI
            args.Append("/no_confirm_del ");       // No delete confirmation (for move)
            args.Append("/no_confirm_stop ");      // No stop confirmation
            
            return args.ToString().Trim();
        }

        /// <summary>
        /// Parse progress information from FastCopy output
        /// </summary>
        private void ParseProgressFromOutput(string output, IProgress<FastCopyProgress> progress)
        {
            if (progress == null || string.IsNullOrEmpty(output))
                return;

            try
            {
                // FastCopy outputs progress in format:
                // Example: "12.34% 123.4MB/s (456/1000 files)"
                // Example: "Copying: filename.txt"
                
                var percentMatch = Regex.Match(output, @"([\d.]+)%");
                if (percentMatch.Success)
                {
                    var percent = double.Parse(percentMatch.Groups[1].Value);
                    
                    var speedMatch = Regex.Match(output, @"([\d.]+)\s*(MB|KB|GB)/s", RegexOptions.IgnoreCase);
                    double speed = 0;
                    if (speedMatch.Success)
                    {
                        speed = double.Parse(speedMatch.Groups[1].Value);
                        var unit = speedMatch.Groups[2].Value.ToUpper();
                        
                        // Convert to MB/s
                        if (unit == "KB") speed /= 1024;
                        else if (unit == "GB") speed *= 1024;
                    }

                    var filesMatch = Regex.Match(output, @"\((\d+)/(\d+) files\)");
                    int processedFiles = 0;
                    int totalFiles = 0;
                    if (filesMatch.Success)
                    {
                        processedFiles = int.Parse(filesMatch.Groups[1].Value);
                        totalFiles = int.Parse(filesMatch.Groups[2].Value);
                    }

                    progress.Report(new FastCopyProgress
                    {
                        PercentComplete = percent,
                        SpeedMBps = speed,
                        ProcessedFiles = processedFiles,
                        TotalFiles = totalFiles,
                        StatusText = output.Trim()
                    });
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        /// <summary>
        /// Parse final statistics from FastCopy output
        /// </summary>
        private void ParseStatistics(string output, FastCopyResult result)
        {
            try
            {
                // Parse: "Total: 123 files, 1.23 GB"
                var filesMatch = Regex.Match(output, @"Total:\s*(\d+)\s*files");
                if (filesMatch.Success)
                {
                    result.FilesProcessed = int.Parse(filesMatch.Groups[1].Value);
                }

                var sizeMatch = Regex.Match(output, @"([\d.]+)\s*(GB|MB|KB)", RegexOptions.IgnoreCase);
                if (sizeMatch.Success)
                {
                    var size = double.Parse(sizeMatch.Groups[1].Value);
                    var unit = sizeMatch.Groups[2].Value.ToUpper();
                    
                    // Convert to bytes
                    if (unit == "KB") result.BytesProcessed = (long)(size * 1024);
                    else if (unit == "MB") result.BytesProcessed = (long)(size * 1024 * 1024);
                    else if (unit == "GB") result.BytesProcessed = (long)(size * 1024 * 1024 * 1024);
                }

                // Calculate average speed
                if (result.Duration.TotalSeconds > 0)
                {
                    result.AverageSpeedMBps = result.BytesProcessed / (1024.0 * 1024.0) / result.Duration.TotalSeconds;
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }
    }

    /// <summary>
    /// Progress information from FastCopy
    /// </summary>
    public class FastCopyProgress
    {
        public double PercentComplete { get; set; }
        public double SpeedMBps { get; set; }
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public string StatusText { get; set; }
    }

    /// <summary>
    /// Result of FastCopy operation
    /// </summary>
    public class FastCopyResult
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string ErrorOutput { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int FilesProcessed { get; set; }
        public long BytesProcessed { get; set; }
        public double AverageSpeedMBps { get; set; }
    }
}
