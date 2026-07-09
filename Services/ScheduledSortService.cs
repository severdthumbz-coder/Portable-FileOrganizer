using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Runs a rule-based organization sweep of the configured folders on a recurring
    /// interval. Unlike the watcher (which reacts to new files), this processes files
    /// already present. Uses a DispatcherTimer so callbacks land on the UI thread; the
    /// actual file work is awaited off-thread inside the tick.
    /// </summary>
    public class ScheduledSortService : IDisposable
    {
        private readonly RuleEngine _ruleEngine;
        private readonly MoveEngine _moveEngine;
        private readonly DispatcherTimer _timer;
        private bool _busy;

        private List<string> _folders = new List<string>();
        private bool _includeSubdirectories;

        public bool IsRunning { get; private set; }
        public DateTime? LastRun { get; private set; }
        public DateTime? NextRun { get; private set; }

        /// <summary>Raised with a human-readable status line for each sweep and file action.</summary>
        public event Action<string> Log;

        public ScheduledSortService(RuleEngine ruleEngine, MoveEngine moveEngine)
        {
            _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
            _moveEngine = moveEngine ?? throw new ArgumentNullException(nameof(moveEngine));
            _timer = new DispatcherTimer();
            _timer.Tick += async (s, e) => await RunSweepAsync(scheduled: true);
        }

        /// <summary>
        /// Starts recurring sweeps every intervalMinutes. Optionally runs one immediately.
        /// </summary>
        public void Start(IEnumerable<string> folders, bool includeSubdirectories,
            int intervalMinutes, bool runImmediately)
        {
            if (intervalMinutes < 1) intervalMinutes = 1;

            _folders = (folders ?? Enumerable.Empty<string>())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .ToList();
            _includeSubdirectories = includeSubdirectories;

            _timer.Interval = TimeSpan.FromMinutes(intervalMinutes);
            _timer.Start();
            IsRunning = true;
            NextRun = DateTime.Now.AddMinutes(intervalMinutes);
            Log?.Invoke($"Scheduler started — every {intervalMinutes} minute(s). Next run ~{NextRun:t}.");

            if (runImmediately)
            {
                _ = RunSweepAsync(scheduled: false);
            }
        }

        public void Stop()
        {
            _timer.Stop();
            IsRunning = false;
            NextRun = null;
            Log?.Invoke("Scheduler stopped.");
        }

        /// <summary>Runs a single sweep on demand (e.g. a "Run Now" button).</summary>
        public Task RunNowAsync() => RunSweepAsync(scheduled: false);

        private async Task RunSweepAsync(bool scheduled)
        {
            if (_busy)
            {
                Log?.Invoke("Skipped sweep — previous sweep still running.");
                return;
            }

            _busy = true;
            try
            {
                LastRun = DateTime.Now;
                if (scheduled && _timer.IsEnabled)
                    NextRun = DateTime.Now.Add(_timer.Interval);

                int organized = 0, skipped = 0, failed = 0;

                foreach (var folder in _folders)
                {
                    if (!Directory.Exists(folder))
                    {
                        Log?.Invoke($"Sweep: folder not found: {folder}");
                        continue;
                    }

                    var option = _includeSubdirectories
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;

                    string[] files;
                    try
                    {
                        files = Directory.GetFiles(folder, "*", option);
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"Sweep: could not list {folder}: {ex.Message}");
                        continue;
                    }

                    foreach (var file in files)
                    {
                        var match = await _ruleEngine.EvaluateAsync(file);
                        if (!match.Matched) continue;

                        var result = await _moveEngine.OrganizeFileAsync(
                            file, match.DestinationPath, match.Rule.IsMove, match.Rule.ConflictResolution);

                        if (result.Success)
                        {
                            if (!string.IsNullOrEmpty(result.ErrorMessage) &&
                                result.ErrorMessage.StartsWith("Skipped"))
                                skipped++;
                            else
                                organized++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                }

                Log?.Invoke($"Sweep complete — organized {organized}, skipped {skipped}, failed {failed}.");
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Sweep error: {ex.Message}");
            }
            finally
            {
                _busy = false;
            }
        }

        public void Dispose()
        {
            try { _timer.Stop(); } catch { }
        }
    }
}
