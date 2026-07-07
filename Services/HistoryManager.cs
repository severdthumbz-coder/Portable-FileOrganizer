using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Manages operation history persistence
    /// </summary>
    public class HistoryManager
    {
        private readonly string _historyPath;
        private const string HistoryFileName = "history.json";
        private const int MaxHistoryEntries = 100;

        public HistoryManager()
        {
            // Store history in user's AppData folder
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PortableFileOrganizer"
            );

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _historyPath = Path.Combine(appDataPath, HistoryFileName);
        }

        /// <summary>
        /// Saves history entries to JSON file
        /// </summary>
        public bool SaveHistory(List<HistoryEntry> history)
        {
            try
            {
                // Keep only the most recent entries
                var entriesToSave = history
                    .OrderByDescending(h => h.Timestamp)
                    .Take(MaxHistoryEntries)
                    .ToList();

                var json = JsonConvert.SerializeObject(entriesToSave, Formatting.Indented);
                File.WriteAllText(_historyPath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving history: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads history entries from JSON file
        /// </summary>
        public List<HistoryEntry> LoadHistory()
        {
            try
            {
                if (!File.Exists(_historyPath))
                {
                    return new List<HistoryEntry>();
                }

                var json = File.ReadAllText(_historyPath);
                var history = JsonConvert.DeserializeObject<List<HistoryEntry>>(json);
                return history ?? new List<HistoryEntry>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading history: {ex.Message}");
                return new List<HistoryEntry>();
            }
        }

        /// <summary>
        /// Adds a new history entry
        /// </summary>
        public bool AddHistoryEntry(HistoryEntry entry, List<HistoryEntry> currentHistory)
        {
            try
            {
                currentHistory.Insert(0, entry); // Add at the beginning (most recent)
                return SaveHistory(currentHistory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding history entry: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clears all history
        /// </summary>
        public bool ClearHistory()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    File.Delete(_historyPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing history: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the full path to the history file
        /// </summary>
        public string GetHistoryPath()
        {
            return _historyPath;
        }
    }
}
