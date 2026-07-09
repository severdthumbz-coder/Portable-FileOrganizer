using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Watches one or more folders and, when a new file settles, evaluates it against
    /// the RuleEngine and organizes it via MoveEngine.OrganizeFileAsync. Designed to be
    /// started/stopped from the UI. All heavy work happens off the FileSystemWatcher
    /// thread; UI updates are surfaced through the Log event.
    /// </summary>
    public class FolderWatcherService : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private readonly RuleEngine _ruleEngine;
        private readonly MoveEngine _moveEngine;

        // Debounce: files often fire multiple events while still being written.
        // We wait for the file to be readable (not locked) before acting.
        private readonly int _settleDelayMs;
        private readonly int _maxSettleAttempts;

        public bool IsRunning { get; private set; }

        /// <summary>Raised with a human-readable status line (file organized, skipped, error).</summary>
        public event Action<string> Log;

        public FolderWatcherService(RuleEngine ruleEngine, MoveEngine moveEngine,
            int settleDelayMs = 750, int maxSettleAttempts = 20)
        {
            _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
            _moveEngine = moveEngine ?? throw new ArgumentNullException(nameof(moveEngine));
            _settleDelayMs = settleDelayMs;
            _maxSettleAttempts = maxSettleAttempts;
        }

        /// <summary>
        /// Begins watching the given folders. Existing watchers are cleared first.
        /// </summary>
        public void Start(IEnumerable<string> folders, bool includeSubdirectories)
        {
            Stop();

            foreach (var folder in folders)
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    Log?.Invoke($"Skipped watch (folder not found): {folder}");
                    continue;
                }

                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = includeSubdirectories,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                watcher.Created += OnChanged;
                watcher.Renamed += OnRenamed;
                _watchers.Add(watcher);
                Log?.Invoke($"Watching: {folder}{(includeSubdirectories ? " (incl. subfolders)" : "")}");
            }

            IsRunning = _watchers.Count > 0;
        }

        public void Stop()
        {
            foreach (var w in _watchers)
            {
                try
                {
                    w.EnableRaisingEvents = false;
                    w.Created -= OnChanged;
                    w.Renamed -= OnRenamed;
                    w.Dispose();
                }
                catch { /* best effort */ }
            }
            _watchers.Clear();
            IsRunning = false;
        }

        private void OnRenamed(object sender, RenamedEventArgs e) => HandleFile(e.FullPath);
        private void OnChanged(object sender, FileSystemEventArgs e) => HandleFile(e.FullPath);

        private void HandleFile(string path)
        {
            // Fire-and-forget; each file is processed independently.
            _ = Task.Run(() => ProcessFileAsync(path));
        }

        private async Task ProcessFileAsync(string path)
        {
            try
            {
                // Ignore directories.
                if (Directory.Exists(path)) return;

                // Wait until the file is done being written (not locked).
                if (!await WaitUntilReadyAsync(path))
                {
                    Log?.Invoke($"Timed out waiting for file to be ready: {Path.GetFileName(path)}");
                    return;
                }

                var match = await _ruleEngine.EvaluateAsync(path);
                if (!match.Matched)
                {
                    // No rule applied — leave the file where it is.
                    return;
                }

                var result = await _moveEngine.OrganizeFileAsync(
                    path,
                    match.DestinationPath,
                    match.Rule.IsMove,
                    match.Rule.ConflictResolution);

                if (result.Success)
                {
                    Log?.Invoke($"{(match.Rule.IsMove ? "Moved" : "Copied")} \"{Path.GetFileName(path)}\" → {match.Rule.DestinationFolder} [rule: {match.Rule.Name}]");
                }
                else
                {
                    Log?.Invoke($"Failed \"{Path.GetFileName(path)}\": {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Watcher error on {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        /// <summary>
        /// Polls the file until it can be opened exclusively (i.e., the writer has
        /// released it), or gives up after maxSettleAttempts.
        /// </summary>
        private async Task<bool> WaitUntilReadyAsync(string path)
        {
            for (int i = 0; i < _maxSettleAttempts; i++)
            {
                try
                {
                    if (!File.Exists(path)) return false;
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        return true; // opened exclusively -> writer is done
                    }
                }
                catch (IOException)
                {
                    await Task.Delay(_settleDelayMs);
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        public void Dispose() => Stop();
    }
}
