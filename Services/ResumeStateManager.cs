using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Manages resume state for interrupted file operations
    /// </summary>
    public class ResumeStateManager
    {
        private readonly string _resumeStatePath;
        private const string ResumeStateFileName = "resume_state.json";
        private const string TempResumeStateFileName = "resume_state.tmp";

        public ResumeStateManager()
        {
            // Store resume state in user's AppData folder
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PortableFileOrganizer"
            );

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _resumeStatePath = Path.Combine(appDataPath, ResumeStateFileName);
        }

        /// <summary>
        /// Saves resume state to disk atomically
        /// </summary>
        public bool SaveState(ResumeState state)
        {
            if (state == null)
                return false;

            try
            {
                var directory = Path.GetDirectoryName(_resumeStatePath);
                var tempPath = Path.Combine(directory, TempResumeStateFileName);

                // Write to temporary file first
                var json = JsonConvert.SerializeObject(state, Formatting.Indented);
                File.WriteAllText(tempPath, json);

                // Atomic replace - if this crashes, we still have the old state
                if (File.Exists(_resumeStatePath))
                {
                    File.Delete(_resumeStatePath);
                }
                File.Move(tempPath, _resumeStatePath);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving resume state: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads resume state from disk
        /// </summary>
        public ResumeState LoadState()
        {
            try
            {
                if (!File.Exists(_resumeStatePath))
                {
                    return null;
                }

                var json = File.ReadAllText(_resumeStatePath);
                var state = JsonConvert.DeserializeObject<ResumeState>(json);

                // Validate state
                if (state == null || state.RemainingQueue == null || state.RemainingQueue.Count == 0)
                {
                    // Invalid or empty state, clean it up
                    ClearState();
                    return null;
                }

                return state;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading resume state: {ex.Message}");
                
                // If state file is corrupted, delete it
                try
                {
                    if (File.Exists(_resumeStatePath))
                    {
                        File.Delete(_resumeStatePath);
                    }
                }
                catch { }
                
                return null;
            }
        }

        /// <summary>
        /// Checks if there's an incomplete operation
        /// </summary>
        public bool HasIncompleteOperation()
        {
            return File.Exists(_resumeStatePath);
        }

        /// <summary>
        /// Clears the resume state file
        /// </summary>
        public bool ClearState()
        {
            try
            {
                if (File.Exists(_resumeStatePath))
                {
                    File.Delete(_resumeStatePath);
                }

                // Also delete temp file if it exists
                var directory = Path.GetDirectoryName(_resumeStatePath);
                var tempPath = Path.Combine(directory, TempResumeStateFileName);
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing resume state: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates the resume state with completed files
        /// </summary>
        public bool UpdateState(ResumeState state, List<QueueEntry> processedEntries)
        {
            if (state == null || processedEntries == null)
                return false;

            try
            {
                // Add newly completed files
                foreach (var entry in processedEntries.Where(e => e.Status == "Moved" || e.Status == "Copied"))
                {
                    if (!state.CompletedFiles.Contains(entry.SourcePath))
                    {
                        state.CompletedFiles.Add(entry.SourcePath);
                        state.CompletedCount++;
                    }
                }

                // Remove completed files from remaining queue
                state.RemainingQueue.RemoveAll(e => 
                    state.CompletedFiles.Contains(e.SourcePath));

                // Update timestamp
                state.InterruptedAt = DateTime.Now;

                return SaveState(state);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating resume state: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a new resume state from a queue
        /// </summary>
        public ResumeState CreateState(
            FileOperationMode operationMode,
            string sourceFolder,
            string destinationFolder,
            List<QueueEntry> queue)
        {
            return new ResumeState
            {
                InterruptedAt = DateTime.Now,
                OperationMode = operationMode,
                SourceFolder = sourceFolder,
                DestinationFolder = destinationFolder,
                RemainingQueue = new List<QueueEntry>(queue),
                TotalFiles = queue.Count,
                CompletedCount = 0,
                CompletedFiles = new List<string>()
            };
        }

        /// <summary>
        /// Gets the full path to the resume state file
        /// </summary>
        public string GetResumeStatePath()
        {
            return _resumeStatePath;
        }

        /// <summary>
        /// Validates that resume state is still valid (files exist, etc.)
        /// </summary>
        public bool ValidateState(ResumeState state)
        {
            if (state == null)
                return false;

            try
            {
                // Check if source folder still exists
                if (!string.IsNullOrEmpty(state.SourceFolder) && !Directory.Exists(state.SourceFolder))
                {
                    return false;
                }

                // Check if destination folder still exists
                if (!string.IsNullOrEmpty(state.DestinationFolder) && !Directory.Exists(state.DestinationFolder))
                {
                    return false;
                }

                // Check if there are remaining files to process
                if (state.RemainingQueue == null || state.RemainingQueue.Count == 0)
                {
                    return false;
                }

                // Validate that at least some source files still exist
                int existingFiles = 0;
                foreach (var entry in state.RemainingQueue.Take(10)) // Check first 10 files
                {
                    if (File.Exists(entry.SourcePath))
                    {
                        existingFiles++;
                    }
                }

                // If none of the first 10 files exist, state is probably invalid
                return existingFiles > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating resume state: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a summary of the resume state for display
        /// </summary>
        public ResumeSummary GetStateSummary(ResumeState state)
        {
            if (state == null)
                return null;

            return new ResumeSummary
            {
                OperationMode = state.OperationMode.ToString(),
                InterruptedAt = state.InterruptedAt,
                SourceFolder = state.SourceFolder,
                DestinationFolder = state.DestinationFolder,
                TotalFiles = state.TotalFiles,
                CompletedFiles = state.CompletedCount,
                RemainingFiles = state.RemainingQueue?.Count ?? 0,
                PercentComplete = state.TotalFiles > 0 
                    ? (double)state.CompletedCount / state.TotalFiles * 100 
                    : 0
            };
        }
    }

    /// <summary>
    /// Summary of resume state for display
    /// </summary>
    public class ResumeSummary
    {
        public string OperationMode { get; set; }
        public DateTime InterruptedAt { get; set; }
        public string SourceFolder { get; set; }
        public string DestinationFolder { get; set; }
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public int RemainingFiles { get; set; }
        public double PercentComplete { get; set; }

        public string TimeSinceInterruption
        {
            get
            {
                var elapsed = DateTime.Now - InterruptedAt;
                if (elapsed.TotalMinutes < 1)
                    return "less than a minute ago";
                if (elapsed.TotalHours < 1)
                    return $"{(int)elapsed.TotalMinutes} minute{((int)elapsed.TotalMinutes != 1 ? "s" : "")} ago";
                if (elapsed.TotalDays < 1)
                    return $"{(int)elapsed.TotalHours} hour{((int)elapsed.TotalHours != 1 ? "s" : "")} ago";
                return $"{(int)elapsed.TotalDays} day{((int)elapsed.TotalDays != 1 ? "s" : "")} ago";
            }
        }
    }
}
