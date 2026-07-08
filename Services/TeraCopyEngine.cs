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
    /// Integration with TeraCopy for high-performance file operations
    /// </summary>
    public class TeraCopyEngine
    {
        private readonly string _teraCopyPath;
        private readonly FileConflictResolution _conflictResolution;
        private readonly bool _preserveTimestamps;
        private readonly VerificationMode _verificationMode;

        public TeraCopyEngine(
            string teraCopyPath, 
            FileConflictResolution conflictResolution, 
            bool preserveTimestamps = true,
            VerificationMode verificationMode = VerificationMode.Smart)
        {
            _teraCopyPath = teraCopyPath ?? throw new ArgumentNullException(nameof(teraCopyPath));
            _conflictResolution = conflictResolution;
            _preserveTimestamps = preserveTimestamps;
            _verificationMode = verificationMode;

            if (!File.Exists(_teraCopyPath))
            {
                throw new FileNotFoundException($"TeraCopy not found at: {_teraCopyPath}");
            }
        }

        /// <summary>
        /// Copy a file using TeraCopy
        /// </summary>
        public async Task<TeraCopyResult> CopyFileAsync(
            string sourcePath,
            string destinationPath,
            IProgress<TeraCopyProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteTeraCopyOperation(
                "Copy",
                sourcePath,
                destinationPath,
                progress,
                cancellationToken);
        }

        /// <summary>
        /// Move a file using TeraCopy
        /// </summary>
        public async Task<TeraCopyResult> MoveFileAsync(
            string sourcePath,
            string destinationPath,
            IProgress<TeraCopyProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteTeraCopyOperation(
                "Move",
                sourcePath,
                destinationPath,
                progress,
                cancellationToken);
        }

        /// <summary>
        /// Execute TeraCopy operation via command line
        /// </summary>
        private async Task<TeraCopyResult> ExecuteTeraCopyOperation(
            string operation,
            string sourcePath,
            string destinationPath,
            IProgress<TeraCopyProgress> progress,
            CancellationToken cancellationToken)
        {
            var result = new TeraCopyResult
            {
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                Success = false,
                StartTime = DateTime.Now
            };

            try
            {
                // Build TeraCopy command line arguments
                var arguments = BuildCommandLineArguments(operation, sourcePath, destinationPath);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _teraCopyPath,
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

                    // TeraCopy exit codes: 0 = success, 1 = some files failed, 2+ = critical error
                    result.Success = process.ExitCode == 0;

                    if (!result.Success)
                    {
                        result.ErrorMessage = !string.IsNullOrEmpty(result.ErrorOutput) 
                            ? result.ErrorOutput 
                            : $"TeraCopy operation failed with exit code {result.ExitCode}";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"TeraCopy execution error: {ex.Message}";
                result.EndTime = DateTime.Now;
                result.Duration = result.EndTime - result.StartTime;
            }

            return result;
        }

        /// <summary>
        /// Build command line arguments for TeraCopy
        /// </summary>
        private string BuildCommandLineArguments(string operation, string sourcePath, string destinationPath)
        {
            var destFolder = Path.GetDirectoryName(destinationPath);
            var destFileName = Path.GetFileName(destinationPath);
            
            // TeraCopy command format:
            // TeraCopy.exe Copy|Move "source" "destination_folder" [options]
            
            var args = new StringBuilder();
            args.Append($"{operation} ");
            args.Append($"\"{sourcePath}\" ");
            args.Append($"\"{destFolder}\" ");

            // Conflict resolution options
            switch (_conflictResolution)
            {
                case FileConflictResolution.Skip:
                    args.Append("/SkipAll ");
                    break;
                case FileConflictResolution.Overwrite:
                    args.Append("/OverwriteAll ");
                    break;
                case FileConflictResolution.OverwriteIfNewer:
                    args.Append("/OverwriteOlder ");
                    break;
                case FileConflictResolution.RenameKeepBoth:
                    args.Append("/RenameAll ");
                    break;
            }

            // Additional options.
            // NOTE: /Close and /NoClose are mutually exclusive; emitting both is undefined.
            // We run headless and read the process result, so keep the window from lingering
            // with /Close and suppress the GUI with /Silent.
            args.Append("/Close ");          // Close TeraCopy automatically when done
            args.Append("/Silent ");         // No GUI prompts
            
            // Preserve timestamps if enabled
            if (_preserveTimestamps)
            {
                args.Append("/PreserveTimestamp ");
            }
            
            // Verification if enabled (not None mode)
            if (_verificationMode != VerificationMode.None)
            {
                args.Append("/Test ");       // Verify files after copy
            }
            
            return args.ToString().Trim();
        }

        /// <summary>
        /// Parse progress information from TeraCopy output
        /// </summary>
        private void ParseProgressFromOutput(string output, IProgress<TeraCopyProgress> progress)
        {
            if (progress == null || string.IsNullOrEmpty(output))
                return;

            try
            {
                // TeraCopy outputs progress in various formats
                // Example: "Copying: filename.txt (45%)"
                // Example: "45% - 123.4 MB/s"
                
                var percentMatch = Regex.Match(output, @"(\d+)%");
                if (percentMatch.Success)
                {
                    var percent = int.Parse(percentMatch.Groups[1].Value);
                    
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

                    progress.Report(new TeraCopyProgress
                    {
                        PercentComplete = percent,
                        SpeedMBps = speed,
                        StatusText = output.Trim()
                    });
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }
    }

    /// <summary>
    /// Progress information from TeraCopy
    /// </summary>
    public class TeraCopyProgress
    {
        public int PercentComplete { get; set; }
        public double SpeedMBps { get; set; }
        public string StatusText { get; set; }
    }

    /// <summary>
    /// Result of TeraCopy operation
    /// </summary>
    public class TeraCopyResult
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
    }
}
