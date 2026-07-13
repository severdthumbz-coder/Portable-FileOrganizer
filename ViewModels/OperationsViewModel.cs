using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FileOrganizer.Commands;
using FileOrganizer.Models;
using FileOrganizer.Services;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Owns the Operations tab and the core file pipeline: the queue, scan (full + quick),
    /// dry run, live move/copy, undo, resume, and the live progress / performance display.
    ///
    /// This is the heart of the application. It depends only on the narrow abstractions built
    /// during the refactor, never on MainViewModel:
    ///   INotificationService        — status line + completion banner
    ///   SessionContext              — source/destination folders + operation mode
    ///   HistoryViewModel            — records each operation
    ///   IStatsSink                  — updates the Statistics read-model
    ///   ToastNotificationService    — start/failure toasts
    ///   FileScanner                 — directory scanning
    ///   ResumeStateManager          — crash-resume persistence
    ///   IOperationsSettingsProvider — Configuration-tab settings + exception filtering
    /// </summary>
    public class OperationsViewModel : ViewModelBase
    {
        private const int FilesPerStateSave = 10; // Save resume state every 10 files

        private readonly INotificationService _notify;
        private readonly SessionContext _session;
        private readonly HistoryViewModel _history;
        private readonly IStatsSink _stats;
        private readonly ToastNotificationService _toast;
        private readonly FileScanner _scanner;
        private readonly ResumeStateManager _resumeStateManager;
        private readonly IOperationsSettingsProvider _settings;

        private List<QueueEntry> _lastMoveOperation = new List<QueueEntry>();
        private ResumeState _currentResumeState = null;
        private int _filesProcessedSinceLastSave = 0;

        public OperationsViewModel(
            INotificationService notify,
            SessionContext session,
            HistoryViewModel history,
            IStatsSink stats,
            ToastNotificationService toast,
            FileScanner scanner,
            ResumeStateManager resumeStateManager,
            IOperationsSettingsProvider settings)
        {
            _notify = notify ?? throw new ArgumentNullException(nameof(notify));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _toast = toast ?? throw new ArgumentNullException(nameof(toast));
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            _resumeStateManager = resumeStateManager ?? throw new ArgumentNullException(nameof(resumeStateManager));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            InitialScanCommand = new RelayCommand(_ => InitialScan());
            QuickScanCommand = new RelayCommand(_ => QuickScan());
            UndoCommand = new RelayCommand(_ => Undo(), _ => CanUndo());
            DryRunCommand = new RelayCommand(_ => DryRun());
            LiveMoveCommand = new RelayCommand(_ => LiveMove());
            LiveCopyCommand = new RelayCommand(_ => LiveCopy());
            ClearQueueCommand = new RelayCommand(_ => ClearQueue());

            // React to session operation-mode changes for the Live button visibility.
            _session.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SessionContext.OperationMode))
                {
                    OnPropertyChanged(nameof(ShowLiveMoveButton));
                    OnPropertyChanged(nameof(ShowLiveCopyButton));
                }
            };
        }

        // Convenience accessors onto the session (keeps the method bodies readable).
        private string SourceFolder => _session.SourceFolder;
        private string DestinationFolder => _session.DestinationFolder;
        private FileOperationMode OperationMode => _session.OperationMode;

        // ---- Queue ----
        public ObservableCollection<QueueEntry> FileQueue { get; } = new ObservableCollection<QueueEntry>();

        private int _pendingCount = 0;
        public int PendingCount
        {
            get => _pendingCount;
            set => SetProperty(ref _pendingCount, value);
        }

        private int _movedCount = 0;
        public int MovedCount
        {
            get => _movedCount;
            set => SetProperty(ref _movedCount, value);
        }

        private int _failedCount = 0;
        public int FailedCount
        {
            get => _failedCount;
            set => SetProperty(ref _failedCount, value);
        }

        // ---- Progress + live performance metrics ----
        private double _progressValue = 0;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private bool _showPerformanceMonitor;
        public bool ShowPerformanceMonitor
        {
            get => _showPerformanceMonitor;
            set => SetProperty(ref _showPerformanceMonitor, value);
        }

        private string _transferSpeedDisplay = "—";
        public string TransferSpeedDisplay
        {
            get => _transferSpeedDisplay;
            set => SetProperty(ref _transferSpeedDisplay, value);
        }

        private string _etaDisplay = "—";
        public string EtaDisplay
        {
            get => _etaDisplay;
            set => SetProperty(ref _etaDisplay, value);
        }

        private string _filesPerSecondDisplay = "—";
        public string FilesPerSecondDisplay
        {
            get => _filesPerSecondDisplay;
            set => SetProperty(ref _filesPerSecondDisplay, value);
        }

        private string _dataProgressDisplay = "—";
        public string DataProgressDisplay
        {
            get => _dataProgressDisplay;
            set => SetProperty(ref _dataProgressDisplay, value);
        }

        private string _currentFileDisplay = "";
        public string CurrentFileDisplay
        {
            get => _currentFileDisplay;
            set => SetProperty(ref _currentFileDisplay, value);
        }

        private string _lastOperationDuration = "";
        public string LastOperationDuration
        {
            get => _lastOperationDuration;
            set => SetProperty(ref _lastOperationDuration, value);
        }

        public bool ShowLiveMoveButton => OperationMode == FileOperationMode.Move;
        public bool ShowLiveCopyButton => OperationMode == FileOperationMode.Copy;

        // ---- Commands ----
        public ICommand InitialScanCommand { get; }
        public ICommand QuickScanCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand DryRunCommand { get; }
        public ICommand LiveMoveCommand { get; }
        public ICommand LiveCopyCommand { get; }
        public ICommand ClearQueueCommand { get; }

        // ---- Formatting helpers ----
        private static string FormatBytes(double bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            int i = 0;
            while (bytes >= 1024 && i < units.Length - 1) { bytes /= 1024; i++; }
            return $"{bytes:0.##} {units[i]}";
        }

        private static string FormatEta(TimeSpan? span)
        {
            if (span == null) return "—";
            var t = span.Value;
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s";
            if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds}s";
            return $"{t.Seconds}s";
        }

        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
            if (duration.TotalMinutes >= 1)
                return $"{duration.Minutes}m {duration.Seconds}s";
            if (duration.TotalSeconds >= 1)
                return $"{duration.Seconds}.{duration.Milliseconds / 100}s";
            return $"{duration.Milliseconds}ms";
        }

        private void UpdatePerformanceMetrics(OperationProgress p)
        {
            ShowPerformanceMonitor = true;
            TransferSpeedDisplay = $"{FormatBytes(p.BytesPerSecond)}/s";
            EtaDisplay = FormatEta(p.EstimatedRemaining);
            FilesPerSecondDisplay = $"{p.FilesPerSecond:0.0} files/s";
            DataProgressDisplay = $"{FormatBytes(p.BytesProcessed)} / {FormatBytes(p.TotalBytes)}";
            CurrentFileDisplay = p.CurrentFile ?? "";
        }

        // ---- Scans ----
        private async void InitialScan()
        {
            if (string.IsNullOrEmpty(SourceFolder))
            {
                System.Windows.MessageBox.Show("Please select a source folder first.",
                    "No Source Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!System.IO.Directory.Exists(SourceFolder))
            {
                System.Windows.MessageBox.Show("Source folder does not exist.",
                    "Invalid Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _notify.SetStatus("Running initial scan...");
            FileQueue.Clear();
            PendingCount = 0;
            MovedCount = 0;
            FailedCount = 0;

            _toast.ShowOperationStarted("Initial Scan", $"Scanning {SourceFolder}");

            try
            {
                var progress = new Progress<double>(percent =>
                {
                    ProgressValue = percent;
                    _notify.SetStatus($"Scanning... {percent:F1}% complete");
                });

                var results = await _scanner.ScanDirectoryAsync(SourceFolder, _settings.SelectedScanMode, progress);

                results = _settings.ApplyExceptionFilters(results);

                foreach (var entry in results)
                {
                    FileQueue.Add(entry);
                }

                stopwatch.Stop();
                LastOperationDuration = FormatDuration(stopwatch.Elapsed);

                PendingCount = results.Count;
                ProgressValue = 0;
                _notify.SetStatus($"Scan complete! Found {results.Count} files in {LastOperationDuration}");

                _notify.ShowCompletionBanner("Initial Scan",
                    $"Found {results.Count} files  |  Duration: {LastOperationDuration}  |  Ready to organize",
                    "🔍");

                _history.AddEntry("Initial Scan", results.Count, results.Count, "Success");
                _stats.IncrementOperations();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LastOperationDuration = FormatDuration(stopwatch.Elapsed);

                ProgressValue = 0;
                _notify.SetStatus($"Scan failed: {ex.Message}");

                _toast.ShowOperationFailed("Initial Scan", ex.Message);

                System.Windows.MessageBox.Show($"Error during scan:\n{ex.Message}",
                    "Scan Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

                _history.AddEntry("Initial Scan", 0, 0, "Failed");
            }
        }

        private void QuickScan()
        {
            if (string.IsNullOrEmpty(SourceFolder))
            {
                System.Windows.MessageBox.Show("Please select a source folder first.",
                    "No Source Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!System.IO.Directory.Exists(SourceFolder))
            {
                System.Windows.MessageBox.Show("Source folder does not exist.",
                    "Invalid Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _notify.SetStatus("Running quick scan (top-level only)...");
            FileQueue.Clear();
            PendingCount = 0;
            MovedCount = 0;
            FailedCount = 0;

            _toast.ShowOperationStarted("Quick Scan", $"Scanning {SourceFolder} (top-level only)");

            try
            {
                var results = _scanner.QuickScan(SourceFolder);

                results = _settings.ApplyExceptionFilters(results);

                foreach (var entry in results)
                {
                    FileQueue.Add(entry);
                }

                stopwatch.Stop();
                LastOperationDuration = FormatDuration(stopwatch.Elapsed);

                PendingCount = results.Count;
                _notify.SetStatus($"Quick scan complete! Found {results.Count} files in {LastOperationDuration}");

                _notify.ShowCompletionBanner("Quick Scan",
                    $"Found {results.Count} files in top-level directory  |  Duration: {LastOperationDuration}",
                    "⚡");

                _history.AddEntry("Quick Scan", results.Count, results.Count, "Success");
                _stats.IncrementOperations();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LastOperationDuration = FormatDuration(stopwatch.Elapsed);

                _notify.SetStatus($"Quick scan failed: {ex.Message}");

                _toast.ShowOperationFailed("Quick Scan", ex.Message);

                System.Windows.MessageBox.Show($"Error during quick scan:\n{ex.Message}",
                    "Scan Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

                _history.AddEntry("Quick Scan", 0, 0, "Failed");
            }
        }

        // ---- Undo ----
        private bool CanUndo()
        {
            return _lastMoveOperation != null && _lastMoveOperation.Count > 0;
        }

        private async void Undo()
        {
            if (_lastMoveOperation == null || _lastMoveOperation.Count == 0)
            {
                _notify.ShowCompletionBanner("Nothing to Undo",
                    "No recent move operation found. Move files first to enable undo functionality.",
                    "ℹ️");
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"This will attempt to undo the last move operation ({_lastMoveOperation.Count} files).\n\nContinue?",
                "Confirm Undo",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            _notify.SetStatus("Undoing last operation...");
            int successCount = 0;
            int failedCount = 0;

            try
            {
                for (int i = 0; i < _lastMoveOperation.Count; i++)
                {
                    var entry = _lastMoveOperation[i];

                    try
                    {
                        if (entry.Status == "Moved" && !string.IsNullOrEmpty(entry.DestinationPath))
                        {
                            if (System.IO.File.Exists(entry.DestinationPath))
                            {
                                var sourceDir = System.IO.Path.GetDirectoryName(entry.SourcePath);
                                if (!System.IO.Directory.Exists(sourceDir))
                                {
                                    System.IO.Directory.CreateDirectory(sourceDir);
                                }

                                System.IO.File.Move(entry.DestinationPath, entry.SourcePath, true);
                                successCount++;
                            }
                            else
                            {
                                failedCount++;
                            }
                        }

                        ProgressValue = ((double)(i + 1) / _lastMoveOperation.Count) * 100;
                        _notify.SetStatus($"Undoing... {ProgressValue:F1}%");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to undo file {entry.FileName}: {ex.Message}");
                        failedCount++;
                    }
                }

                ProgressValue = 0;
                _lastMoveOperation.Clear();

                _notify.SetStatus($"Undo complete! Restored: {successCount}, Failed: {failedCount}");

                _history.AddEntry("Undo Operation", successCount + failedCount, successCount,
                    successCount > 0 ? "Success" : "Failed");
                _stats.IncrementOperations();

                _notify.ShowCompletionBanner("Undo Complete",
                    $"Restored: {successCount} files  |  Failed: {failedCount} files  |  Files returned to original locations",
                    successCount > 0 ? "↩️" : "⚠️");
            }
            catch (Exception ex)
            {
                ProgressValue = 0;
                _notify.SetStatus($"Undo failed: {ex.Message}");
                _notify.ShowCompletionBanner("Undo Error",
                    $"Error during undo operation: {ex.Message}",
                    "❌");
            }
        }

        // ---- Dry run ----
        private void DryRun()
        {
            if (FileQueue.Count == 0)
            {
                System.Windows.MessageBox.Show("No files in queue. Please run a scan first.",
                    "Empty Queue", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(DestinationFolder))
            {
                System.Windows.MessageBox.Show("Please select a destination folder first.",
                    "No Destination", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            _notify.SetStatus("Running dry run (preview)...");

            try
            {
                int wouldMove = 0;
                int wouldSkip = 0;
                long totalBytes = 0;

                foreach (var entry in FileQueue)
                {
                    var destPath = BuildPreviewDestPath(entry);

                    if (System.IO.File.Exists(destPath))
                    {
                        if (_settings.ConflictResolution == FileConflictResolution.Skip)
                        {
                            wouldSkip++;
                            continue;
                        }
                    }

                    wouldMove++;
                    totalBytes += entry.SizeBytes;
                }

                _history.AddEntry("Dry Run", FileQueue.Count, wouldMove, "Success");
                _stats.IncrementOperations();

                double sizeGB = totalBytes / (1024.0 * 1024.0 * 1024.0);
                _notify.SetStatus($"Dry run complete! Would process {wouldMove} files ({sizeGB:F2} GB).");

                System.Windows.MessageBox.Show(
                    $"Dry Run Preview:\n\n" +
                    $"Total files in queue: {FileQueue.Count}\n" +
                    $"Would {OperationMode.ToString().ToLower()}: {wouldMove}\n" +
                    $"Would skip: {wouldSkip}\n" +
                    $"Total size: {sizeGB:F2} GB\n" +
                    $"Structure: {_settings.StructureMode}\n" +
                    $"Conflict handling: {_settings.ConflictResolution}\n\n" +
                    $"No files were actually moved or copied.",
                    "Dry Run Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _notify.SetStatus($"Dry run failed: {ex.Message}");
                System.Windows.MessageBox.Show($"Error during dry run:\n{ex.Message}",
                    "Dry Run Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private string BuildPreviewDestPath(QueueEntry entry)
        {
            var fileName = System.IO.Path.GetFileName(entry.SourcePath);

            switch (_settings.StructureMode)
            {
                case DestinationStructureMode.OrganizeByCategory:
                    return System.IO.Path.Combine(DestinationFolder, entry.Category, fileName);

                case DestinationStructureMode.PreserveStructure:
                    var sourceDir = System.IO.Path.GetDirectoryName(entry.SourcePath);
                    if (!string.IsNullOrEmpty(sourceDir) && !string.IsNullOrEmpty(SourceFolder))
                    {
                        var relativePath = System.IO.Path.GetRelativePath(SourceFolder, sourceDir);
                        if (relativePath != ".")
                        {
                            return System.IO.Path.Combine(DestinationFolder, relativePath, fileName);
                        }
                    }
                    return System.IO.Path.Combine(DestinationFolder, fileName);

                case DestinationStructureMode.Hybrid:
                    var srcDir = System.IO.Path.GetDirectoryName(entry.SourcePath);
                    if (!string.IsNullOrEmpty(srcDir) && !string.IsNullOrEmpty(SourceFolder))
                    {
                        var relPath = System.IO.Path.GetRelativePath(SourceFolder, srcDir);
                        if (relPath != ".")
                        {
                            return System.IO.Path.Combine(DestinationFolder, entry.Category, relPath, fileName);
                        }
                    }
                    return System.IO.Path.Combine(DestinationFolder, entry.Category, fileName);

                default:
                    return System.IO.Path.Combine(DestinationFolder, fileName);
            }
        }

        // ---- Live move ----
        private async void LiveMove()
        {
            if (FileQueue.Count == 0)
            {
                System.Windows.MessageBox.Show("No files in queue. Please run a scan first.",
                    "Empty Queue", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(DestinationFolder))
            {
                System.Windows.MessageBox.Show("Please select a destination folder first.",
                    "No Destination", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"This will MOVE {FileQueue.Count} files from:\n{SourceFolder}\n\nto:\n{DestinationFolder}\n\nFiles will be REMOVED from source. Continue?",
                "Confirm Move Operation",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _notify.SetStatus("Executing live move operation...");

            _toast.ShowOperationStarted("Live Move", $"Moving {FileQueue.Count} files to {DestinationFolder}");

            try
            {
                var config = new Config
                {
                    OperationMode = FileOperationMode.Move,
                    StructureMode = _settings.StructureMode,
                    ConflictResolution = _settings.ConflictResolution,
                    SourceFolder = SourceFolder
                };

                var queueList = FileQueue.ToList();

                _currentResumeState = _resumeStateManager.CreateState(
                    FileOperationMode.Move,
                    SourceFolder,
                    DestinationFolder,
                    queueList);
                _resumeStateManager.SaveState(_currentResumeState);
                _filesProcessedSinceLastSave = 0;

                var engine = new MoveEngine(config);
                var progress = new Progress<OperationProgress>(p =>
                {
                    ProgressValue = p.PercentComplete;
                    _notify.SetStatus($"Moving... {p.PercentComplete:F1}% ({p.ProcessedFiles}/{p.TotalFiles}) - {p.CurrentFile}");
                    MovedCount = p.ProcessedFiles;
                    PendingCount = p.TotalFiles - p.ProcessedFiles;
                    UpdatePerformanceMetrics(p);

                    _filesProcessedSinceLastSave++;
                    if (_filesProcessedSinceLastSave >= FilesPerStateSave)
                    {
                        UpdateResumeState(queueList.Take(p.ProcessedFiles).ToList());
                        _filesProcessedSinceLastSave = 0;
                    }
                });

                Action<string> statusCallback = (message) =>
                {
                    _notify.SetStatus(message);
                };

                var opResult = await engine.ProcessQueueAsync(queueList, DestinationFolder, statusCallback, progress);

                stopwatch.Stop();
                LastOperationDuration = FormatDuration(stopwatch.Elapsed);

                ProgressValue = 0;
                ShowPerformanceMonitor = false;
                MovedCount = opResult.SuccessCount;
                FailedCount = opResult.FailedCount;
                PendingCount = 0;
                _stats.RecordOperation(
                    opResult.SuccessCount,
                    opResult.TotalBytesProcessed / (1024.0 * 1024.0 * 1024.0),
                    opResult.FilesVerified,
                    opResult.VerificationPassed,
                    opResult.VerificationFailed,
                    opResult.VerificationRetried);

                _lastMoveOperation.Clear();
                _lastMoveOperation.AddRange(queueList.Where(e => e.Status == "Moved").ToList());

                _resumeStateManager.ClearState();
                _currentResumeState = null;

                string status = opResult.SuccessCount == opResult.TotalFiles ? "Success" :
                               opResult.SuccessCount > 0 ? $"Partial ({opResult.SuccessCount}/{opResult.TotalFiles})" : "Failed";
                _history.AddEntry("Live Move", opResult.TotalFiles, opResult.SuccessCount, status,
                    opResult.FilesVerified, opResult.VerificationPassed, opResult.VerificationFailed, opResult.VerificationRetried);

                _notify.SetStatus($"Move complete! Success: {opResult.SuccessCount}, Failed: {opResult.FailedCount}, Skipped: {opResult.SkippedCount} in {LastOperationDuration}");

                _notify.ShowCompletionBanner("Move Operation",
                    $"Moved: {opResult.SuccessCount}/{opResult.TotalFiles} files ({opResult.TotalBytesProcessed / (1024.0 * 1024.0 * 1024.0):F2} GB)  |  Failed: {opResult.FailedCount}  |  Skipped: {opResult.SkippedCount}  |  Duration: {LastOperationDuration}",
                    "📦");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LastOperationDuration = FormatDuration(stopwatch.Elapsed);

                _notify.SetStatus($"Move failed: {ex.Message}");

                _toast.ShowOperationFailed("Live Move", ex.Message);

                _history.AddEntry("Live Move", 0, 0, "Failed");

                System.Windows.MessageBox.Show($"Error during move operation:\n{ex.Message}",
                    "Move Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // ---- Live copy ----
        private async void LiveCopy()
        {
            if (FileQueue.Count == 0)
            {
                System.Windows.MessageBox.Show("No files in queue. Please run a scan first.",
                    "Empty Queue", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(DestinationFolder))
            {
                System.Windows.MessageBox.Show("Please select a destination folder first.",
                    "No Destination", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"This will COPY {FileQueue.Count} files from:\n{SourceFolder}\n\nto:\n{DestinationFolder}\n\nOriginals will be kept in source. Continue?",
                "Confirm Copy Operation",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _notify.SetStatus("Executing live copy operation...");

            _toast.ShowOperationStarted("Live Copy", $"Copying {FileQueue.Count} files to {DestinationFolder}");

            try
            {
                var config = new Config
                {
                    OperationMode = FileOperationMode.Copy,
                    StructureMode = _settings.StructureMode,
                    ConflictResolution = _settings.ConflictResolution,
                    SourceFolder = SourceFolder
                };

                var queueList = FileQueue.ToList();

                _currentResumeState = _resumeStateManager.CreateState(
                    FileOperationMode.Copy,
                    SourceFolder,
                    DestinationFolder,
                    queueList);
                _resumeStateManager.SaveState(_currentResumeState);
                _filesProcessedSinceLastSave = 0;

                var engine = new MoveEngine(config);
                var progress = new Progress<OperationProgress>(p =>
                {
                    ProgressValue = p.PercentComplete;
                    _notify.SetStatus($"Copying... {p.PercentComplete:F1}% ({p.ProcessedFiles}/{p.TotalFiles}) - {p.CurrentFile}");
                    MovedCount = p.ProcessedFiles;
                    PendingCount = p.TotalFiles - p.ProcessedFiles;
                    UpdatePerformanceMetrics(p);

                    _filesProcessedSinceLastSave++;
                    if (_filesProcessedSinceLastSave >= FilesPerStateSave)
                    {
                        UpdateResumeState(queueList.Take(p.ProcessedFiles).ToList());
                        _filesProcessedSinceLastSave = 0;
                    }
                });

                Action<string> statusCallback = (message) =>
                {
                    _notify.SetStatus(message);
                };

                var opResult = await engine.ProcessQueueAsync(queueList, DestinationFolder, statusCallback, progress);

                stopwatch.Stop();
                LastOperationDuration = FormatDuration(stopwatch.Elapsed);

                ProgressValue = 0;
                ShowPerformanceMonitor = false;
                MovedCount = opResult.SuccessCount;
                FailedCount = opResult.FailedCount;
                PendingCount = 0;
                _stats.RecordOperation(
                    opResult.SuccessCount,
                    opResult.TotalBytesProcessed / (1024.0 * 1024.0 * 1024.0),
                    opResult.FilesVerified,
                    opResult.VerificationPassed,
                    opResult.VerificationFailed,
                    opResult.VerificationRetried);

                _resumeStateManager.ClearState();
                _currentResumeState = null;

                string status = opResult.SuccessCount == opResult.TotalFiles ? "Success" :
                               opResult.SuccessCount > 0 ? $"Partial ({opResult.SuccessCount}/{opResult.TotalFiles})" : "Failed";
                _history.AddEntry("Live Copy", opResult.TotalFiles, opResult.SuccessCount, status,
                    opResult.FilesVerified, opResult.VerificationPassed, opResult.VerificationFailed, opResult.VerificationRetried);

                _notify.SetStatus($"Copy complete! Success: {opResult.SuccessCount}, Failed: {opResult.FailedCount}, Skipped: {opResult.SkippedCount} in {LastOperationDuration}");

                _notify.ShowCompletionBanner("Copy Operation",
                    $"Copied: {opResult.SuccessCount}/{opResult.TotalFiles} files ({opResult.TotalBytesProcessed / (1024.0 * 1024.0 * 1024.0):F2} GB)  |  Failed: {opResult.FailedCount}  |  Skipped: {opResult.SkippedCount}  |  Duration: {LastOperationDuration}",
                    "📋");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LastOperationDuration = FormatDuration(stopwatch.Elapsed);

                _notify.SetStatus($"Copy failed: {ex.Message}");

                _toast.ShowOperationFailed("Live Copy", ex.Message);

                _history.AddEntry("Live Copy", 0, 0, "Failed");

                System.Windows.MessageBox.Show($"Error during copy operation:\n{ex.Message}",
                    "Copy Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ClearQueue()
        {
            FileQueue.Clear();
            PendingCount = 0;
            MovedCount = 0;
            FailedCount = 0;
            _notify.SetStatus("Queue cleared");
        }

        // ---- Resume ----
        private void UpdateResumeState(List<QueueEntry> processedEntries)
        {
            if (_currentResumeState != null)
            {
                _resumeStateManager.UpdateState(_currentResumeState, processedEntries);
            }
        }

        /// <summary>Resumes an incomplete operation recovered at startup.</summary>
        public async void ResumeOperation(ResumeState state)
        {
            if (state == null || state.RemainingQueue == null || state.RemainingQueue.Count == 0)
            {
                System.Windows.MessageBox.Show("Invalid resume state.",
                    "Resume Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            _notify.SetStatus($"Resuming {state.OperationMode} operation...");

            try
            {
                var config = new Config
                {
                    OperationMode = state.OperationMode,
                    StructureMode = _settings.StructureMode,
                    ConflictResolution = _settings.ConflictResolution,
                    SourceFolder = state.SourceFolder
                };

                _currentResumeState = state;
                _filesProcessedSinceLastSave = 0;

                var engine = new MoveEngine(config);
                var progress = new Progress<OperationProgress>(p =>
                {
                    ProgressValue = p.PercentComplete;
                    _notify.SetStatus($"Resuming... {p.PercentComplete:F1}% ({p.ProcessedFiles}/{p.TotalFiles}) - {p.CurrentFile}");
                    MovedCount = p.ProcessedFiles;
                    PendingCount = p.TotalFiles - p.ProcessedFiles;
                    UpdatePerformanceMetrics(p);

                    _filesProcessedSinceLastSave++;
                    if (_filesProcessedSinceLastSave >= FilesPerStateSave)
                    {
                        UpdateResumeState(state.RemainingQueue.Take(p.ProcessedFiles).ToList());
                        _filesProcessedSinceLastSave = 0;
                    }
                });

                Action<string> statusCallback = (message) =>
                {
                    _notify.SetStatus(message);
                };

                var opResult = await engine.ProcessQueueAsync(state.RemainingQueue, state.DestinationFolder, statusCallback, progress);

                LastOperationDuration = "";
                ProgressValue = 0;
                ShowPerformanceMonitor = false;
                MovedCount = opResult.SuccessCount;
                FailedCount = opResult.FailedCount;
                PendingCount = 0;
                _stats.RecordOperation(
                    opResult.SuccessCount,
                    opResult.TotalBytesProcessed / (1024.0 * 1024.0 * 1024.0),
                    0, 0, 0, 0);

                _resumeStateManager.ClearState();
                _currentResumeState = null;

                string status = opResult.SuccessCount == opResult.TotalFiles ? "Success" :
                               opResult.SuccessCount > 0 ? $"Partial ({opResult.SuccessCount}/{opResult.TotalFiles})" : "Failed";
                _history.AddEntry($"Resume {state.OperationMode}", opResult.TotalFiles, opResult.SuccessCount, status);
                _stats.IncrementOperations();

                _notify.ShowCompletionBanner($"Resume {state.OperationMode} Complete",
                    $"Completed: {opResult.SuccessCount}/{opResult.TotalFiles} files  |  Failed: {opResult.FailedCount}",
                    "▶️");
            }
            catch (Exception ex)
            {
                ProgressValue = 0;
                _notify.SetStatus($"Resume failed: {ex.Message}");
                System.Windows.MessageBox.Show($"Error during resume operation:\n{ex.Message}",
                    "Resume Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Undoes the completed portion of an interrupted Move operation recovered at startup.
        /// Called from the startup resume dialog. Only Move operations can be undone.
        /// </summary>
        public async void UndoFromResume(ResumeState state)
        {
            if (state == null || state.CompletedFiles == null || state.CompletedFiles.Count == 0)
            {
                _notify.ShowCompletionBanner("Nothing to Undo",
                    "No files found to undo from the incomplete operation.",
                    "ℹ️");
                _resumeStateManager.ClearState();
                return;
            }

            _notify.SetStatus("Undoing incomplete operation...");
            int successCount = 0;
            int failedCount = 0;

            try
            {
                if (state.OperationMode != FileOperationMode.Move)
                {
                    _notify.ShowCompletionBanner("Undo Not Available",
                        "Undo is only available for Move operations  |  Copy operations cannot be undone automatically",
                        "ℹ️");
                    _resumeStateManager.ClearState();
                    return;
                }

                for (int i = 0; i < state.CompletedFiles.Count; i++)
                {
                    var sourcePath = state.CompletedFiles[i];

                    try
                    {
                        var entry = state.RemainingQueue.FirstOrDefault(e => e.SourcePath == sourcePath);

                        if (entry != null && !string.IsNullOrEmpty(entry.DestinationPath))
                        {
                            if (System.IO.File.Exists(entry.DestinationPath))
                            {
                                var sourceDir = System.IO.Path.GetDirectoryName(sourcePath);
                                if (!System.IO.Directory.Exists(sourceDir))
                                {
                                    System.IO.Directory.CreateDirectory(sourceDir);
                                }

                                System.IO.File.Move(entry.DestinationPath, sourcePath, true);
                                successCount++;
                            }
                        }

                        ProgressValue = ((double)(i + 1) / state.CompletedFiles.Count) * 100;
                        _notify.SetStatus($"Undoing... {ProgressValue:F1}%");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to undo file {sourcePath}: {ex.Message}");
                        failedCount++;
                    }
                }

                ProgressValue = 0;

                _resumeStateManager.ClearState();
                _currentResumeState = null;

                _notify.SetStatus($"Undo from resume complete! Restored: {successCount}, Failed: {failedCount}");

                _history.AddEntry("Undo from Resume", state.CompletedFiles.Count, successCount,
                    successCount > 0 ? "Success" : "Failed");
                _stats.IncrementOperations();

                _notify.ShowCompletionBanner("Undo Complete",
                    $"Restored: {successCount} files  |  Failed: {failedCount} files  |  Incomplete operation reversed",
                    successCount > 0 ? "↩️" : "⚠️");
            }
            catch (Exception ex)
            {
                ProgressValue = 0;
                _notify.SetStatus($"Undo from resume failed: {ex.Message}");
                _notify.ShowCompletionBanner("Undo Error",
                    $"Error during undo operation: {ex.Message}",
                    "❌");
            }
        }

        /// <summary>
        /// At startup, checks for an interrupted operation left on disk and offers to resume,
        /// undo, or discard it. Called once from MainViewModel's startup.
        /// </summary>
        public void CheckForIncompleteOperation()
        {
            try
            {
                if (_resumeStateManager.HasIncompleteOperation())
                {
                    var state = _resumeStateManager.LoadState();

                    if (state != null && _resumeStateManager.ValidateState(state))
                    {
                        var summary = _resumeStateManager.GetStateSummary(state);
                        var dialog = new FileOrganizer.ResumeDialog(summary);

                        var result = dialog.ShowDialog();

                        if (result == true)
                        {
                            switch (dialog.SelectedAction)
                            {
                                case FileOrganizer.ResumeDialog.ResumeAction.Resume:
                                    // Restore UI state via the shared session.
                                    _session.SourceFolder = state.SourceFolder;
                                    _session.DestinationFolder = state.DestinationFolder;
                                    ResumeOperation(state);
                                    break;

                                case FileOrganizer.ResumeDialog.ResumeAction.Undo:
                                    UndoFromResume(state);
                                    break;

                                case FileOrganizer.ResumeDialog.ResumeAction.Cancel:
                                    _resumeStateManager.ClearState();
                                    _notify.SetStatus("Incomplete operation discarded");
                                    break;
                            }
                        }
                        else
                        {
                            _resumeStateManager.ClearState();
                            _notify.SetStatus("Incomplete operation discarded");
                        }
                    }
                    else
                    {
                        _resumeStateManager.ClearState();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for incomplete operation: {ex.Message}");
                try
                {
                    _resumeStateManager.ClearState();
                }
                catch { }
            }
        }
    }
}
