using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using FileOrganizer.Commands;
using FileOrganizer.Models;
using FileOrganizer.Services;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Owns the Duplicates tab: detection, the duplicate groups, keep-strategy auto-selection,
    /// selection statistics, and the delete / move / export actions.
    ///
    /// Dependencies (all narrow, no reference to MainViewModel):
    ///   INotificationService  — status line + completion banner
    ///   HistoryViewModel      — records detect/delete operations in history
    ///   SessionContext        — reads SourceFolder and the selected ScanMode
    ///   IStatsSink            — updates the Statistics read-model (groups found, wasted space, op count)
    ///   ToastNotificationService — start/failure toasts
    ///
    /// Progress and duration are surfaced through callbacks supplied by the owner, since the
    /// progress bar and the "last operation duration" text are shared UI still owned by
    /// MainViewModel. This keeps the Duplicates tab from owning window-level chrome.
    /// </summary>
    public class DuplicatesViewModel : ViewModelBase
    {
        private readonly INotificationService _notify;
        private readonly HistoryViewModel _history;
        private readonly SessionContext _session;
        private readonly IStatsSink _stats;
        private readonly ToastNotificationService _toast;
        private readonly Action<double> _setProgress;
        private readonly Func<TimeSpan, string> _formatDuration;
        private readonly Action<string> _setLastDuration;
        private readonly Func<ScanMode> _getScanMode;

        public DuplicatesViewModel(
            INotificationService notify,
            HistoryViewModel history,
            SessionContext session,
            IStatsSink stats,
            ToastNotificationService toast,
            Action<double> setProgress,
            Func<TimeSpan, string> formatDuration,
            Action<string> setLastDuration,
            Func<ScanMode> getScanMode)
        {
            _notify = notify ?? throw new ArgumentNullException(nameof(notify));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _toast = toast ?? throw new ArgumentNullException(nameof(toast));
            _setProgress = setProgress ?? (_ => { });
            _formatDuration = formatDuration ?? (ts => ts.ToString());
            _setLastDuration = setLastDuration ?? (_ => { });
            _getScanMode = getScanMode ?? (() => ScanMode.Balanced);

            DetectDuplicatesCommand = new RelayCommand(_ => DetectDuplicates());
            DeleteSelectedDuplicatesCommand = new RelayCommand(_ => DeleteSelectedDuplicates(), _ => HasSelectedDuplicates());
            MoveSelectedDuplicatesCommand = new RelayCommand(_ => MoveSelectedDuplicates(), _ => HasSelectedDuplicates());
            ExportDuplicateListCommand = new RelayCommand(_ => ExportDuplicateList(), _ => DuplicateGroups.Count > 0);
            ClearDuplicateSelectionCommand = new RelayCommand(_ => ClearDuplicateSelection(), _ => HasSelectedDuplicates());
            ToggleGroupExpandCommand = new RelayCommand(param => ToggleGroupExpand(param as DuplicateGroup));
        }

        // ---- State ----
        public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = new ObservableCollection<DuplicateGroup>();

        // These are duplicate-detection results shown on the Duplicates tab. They are ALSO
        // mirrored to the Statistics read-model via IStatsSink so the Statistics tab reflects them.
        private int _duplicateGroupsFound;
        public int DuplicateGroupsFound
        {
            get => _duplicateGroupsFound;
            set
            {
                if (SetProperty(ref _duplicateGroupsFound, value))
                    _stats.DuplicateGroupsFound = value;
            }
        }

        private double _wastedSpaceGB;
        public double WastedSpaceGB
        {
            get => _wastedSpaceGB;
            set
            {
                if (SetProperty(ref _wastedSpaceGB, value))
                    _stats.WastedSpaceGB = value;
            }
        }

        private int _totalDuplicateFiles;
        public int TotalDuplicateFiles
        {
            get => _totalDuplicateFiles;
            set => SetProperty(ref _totalDuplicateFiles, value);
        }

        private bool _useQuickScan;
        public bool UseQuickScan
        {
            get => _useQuickScan;
            set => SetProperty(ref _useQuickScan, value);
        }

        private string _keepStrategy = "None";
        public string KeepStrategy
        {
            get => _keepStrategy;
            set
            {
                if (SetProperty(ref _keepStrategy, value))
                {
                    ApplyAutoSelect();
                }
            }
        }

        private int _selectedForDeletion;
        public int SelectedForDeletion
        {
            get => _selectedForDeletion;
            set => SetProperty(ref _selectedForDeletion, value);
        }

        private double _selectedDeletionSpaceGB;
        public double SelectedDeletionSpaceGB
        {
            get => _selectedDeletionSpaceGB;
            set => SetProperty(ref _selectedDeletionSpaceGB, value);
        }

        // ---- Commands ----
        public ICommand DetectDuplicatesCommand { get; }
        public ICommand DeleteSelectedDuplicatesCommand { get; }
        public ICommand MoveSelectedDuplicatesCommand { get; }
        public ICommand ExportDuplicateListCommand { get; }
        public ICommand ClearDuplicateSelectionCommand { get; }
        public ICommand ToggleGroupExpandCommand { get; }

        private async void DetectDuplicates()
        {
            var sourceFolder = _session.SourceFolder;

            if (string.IsNullOrEmpty(sourceFolder))
            {
                System.Windows.MessageBox.Show("Please select a source folder first.",
                    "No Source Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!System.IO.Directory.Exists(sourceFolder))
            {
                System.Windows.MessageBox.Show("Source folder does not exist.",
                    "Invalid Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _notify.SetStatus("Detecting duplicates...");

            // Send start notification
            _toast.ShowOperationStarted("Duplicate Detection", $"Scanning {sourceFolder} for duplicates");

            try
            {
                var detector = new DuplicateDetector();
                var progress = new Progress<double>(percent =>
                {
                    _setProgress(percent);
                    _notify.SetStatus($"Scanning for duplicates... {percent:F1}% complete");
                });

                // Use quick scan or full scan based on setting
                DuplicateDetectionResult result;
                if (UseQuickScan)
                {
                    _notify.SetStatus("Quick scanning for duplicates (size-based)...");
                    result = await detector.QuickDetectDuplicatesAsync(sourceFolder, SelectedScanMode, progress);
                }
                else
                {
                    _notify.SetStatus("Scanning for duplicates (SHA256)...");
                    result = await detector.DetectDuplicatesAsync(sourceFolder, SelectedScanMode, progress);
                }

                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                _setLastDuration(_formatDuration(duration));

                DuplicateGroupsFound = result.DuplicateGroupCount;
                WastedSpaceGB = result.WastedSpaceGB;
                TotalDuplicateFiles = result.TotalDuplicateFiles;
                _setProgress(0);

                // Populate DuplicateGroups collection for UI
                DuplicateGroups.Clear();
                foreach (var group in result.DuplicateGroups)
                {
                    DuplicateGroups.Add(group);
                }

                // Reset selection statistics
                SelectedForDeletion = 0;
                SelectedDeletionSpaceGB = 0;

                _notify.SetStatus($"Duplicate detection complete! Found {result.DuplicateGroupCount} groups ({result.TotalDuplicateFiles} duplicate files, {result.WastedSpaceGB:F2} GB wasted) in {_formatDuration(duration)}");

                // Show completion banner
                string bannerMessage;
                string bannerIcon;
                if (result.DuplicateGroupCount > 0)
                {
                    bannerMessage = $"Scanned: {result.TotalFilesScanned:N0} files  |  " +
                                   $"Found: {result.DuplicateGroupCount:N0} groups ({result.TotalDuplicateFiles:N0} duplicates)  |  " +
                                   $"Wasted: {result.WastedSpaceGB:F2} GB  |  Duration: {_formatDuration(duration)}\n" +
                                   $"→ View the Duplicates tab to manage them";
                    bannerIcon = "🔍";
                }
                else
                {
                    bannerMessage = $"No duplicates found!  |  Scanned: {result.TotalFilesScanned:N0} files  |  Duration: {_formatDuration(duration)}";
                    bannerIcon = "✨";
                }

                _notify.ShowCompletionBanner("Duplicate Detection", bannerMessage, bannerIcon);

                // Add to history
                _history.AddEntry("Detect Duplicates", result.TotalFilesScanned, result.DuplicateGroupCount, "Success");
                _stats.IncrementOperations();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                _setLastDuration(_formatDuration(duration));

                _setProgress(0);
                _notify.SetStatus($"Duplicate detection failed: {ex.Message}");

                // Send failure notification
                _toast.ShowOperationFailed("Duplicate Detection", ex.Message);

                System.Windows.MessageBox.Show($"Error detecting duplicates:\n{ex.Message}",
                    "Detection Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

                // Add failed history entry
                _history.AddEntry("Detect Duplicates", 0, 0, "Failed");
            }
        }

        private ScanMode SelectedScanMode => _getScanMode();

        private void ApplyAutoSelect()
        {
            if (KeepStrategy == "None" || DuplicateGroups.Count == 0)
            {
                UpdateSelectionStatistics();
                return;
            }

            foreach (var group in DuplicateGroups)
            {
                if (group.DuplicateFiles.Count == 0)
                    continue;

                DuplicateFile keepFile = null;

                switch (KeepStrategy)
                {
                    case "Keep Newest":
                        keepFile = group.DuplicateFiles.OrderByDescending(f => f.ModifiedDate).First();
                        break;

                    case "Keep Oldest":
                        keepFile = group.DuplicateFiles.OrderBy(f => f.ModifiedDate).First();
                        break;

                    case "Keep Shortest Path":
                        keepFile = group.DuplicateFiles.OrderBy(f => f.FilePath.Length).First();
                        break;

                    case "Keep Longest Path":
                        keepFile = group.DuplicateFiles.OrderByDescending(f => f.FilePath.Length).First();
                        break;
                }

                // Mark one to keep, rest to delete
                foreach (var file in group.DuplicateFiles)
                {
                    file.IsSelected = (file != keepFile);
                    file.IsRecommendedKeep = (file == keepFile);
                }
            }

            UpdateSelectionStatistics();
        }

        private void UpdateSelectionStatistics()
        {
            int selectedCount = 0;
            long selectedBytes = 0;

            foreach (var group in DuplicateGroups)
            {
                foreach (var file in group.DuplicateFiles)
                {
                    if (file.IsSelected)
                    {
                        selectedCount++;
                        selectedBytes += file.FileSize;
                    }
                }
            }

            SelectedForDeletion = selectedCount;
            SelectedDeletionSpaceGB = selectedBytes / (1024.0 * 1024.0 * 1024.0);
        }

        private bool HasSelectedDuplicates()
        {
            return DuplicateGroups.Any(g => g.DuplicateFiles.Any(f => f.IsSelected));
        }

        private async void DeleteSelectedDuplicates()
        {
            var selectedFiles = DuplicateGroups
                .SelectMany(g => g.DuplicateFiles)
                .Where(f => f.IsSelected)
                .ToList();

            if (selectedFiles.Count == 0)
            {
                System.Windows.MessageBox.Show("No files selected for deletion.",
                    "No Selection", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            // Verify at least one file is kept in each group
            foreach (var group in DuplicateGroups)
            {
                var selectedInGroup = group.DuplicateFiles.Count(f => f.IsSelected);
                if (selectedInGroup == group.DuplicateFiles.Count)
                {
                    System.Windows.MessageBox.Show(
                        $"Error: All copies of a file cannot be deleted.\n\n" +
                        $"File: {System.IO.Path.GetFileName(group.DuplicateFiles[0].FilePath)}\n\n" +
                        $"At least one copy must be kept in each group.",
                        "Invalid Selection",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }
            }

            // Calculate space to free
            var spaceToFree = selectedFiles.Sum(f => f.FileSize);
            var spaceGB = spaceToFree / (1024.0 * 1024.0 * 1024.0);

            // Confirmation dialog
            var result = System.Windows.MessageBox.Show(
                $"Delete {selectedFiles.Count} duplicate files?\n\n" +
                $"Space to free: {spaceGB:F2} GB\n\n" +
                $"Files will be moved to Recycle Bin and can be restored if needed.",
                "Confirm Deletion",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            // Delete with progress
            _notify.SetStatus("Deleting duplicate files...");
            int deleted = 0;
            int failed = 0;
            var failedFiles = new List<string>();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var file in selectedFiles)
            {
                try
                {
                    // Move to Recycle Bin (safe delete)
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        file.FilePath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

                    deleted++;
                }
                catch (Exception ex)
                {
                    failed++;
                    failedFiles.Add($"{file.FilePath}: {ex.Message}");
                }

                _setProgress(((deleted + failed) / (double)selectedFiles.Count) * 100);
                _notify.SetStatus($"Deleting duplicates... {deleted + failed}/{selectedFiles.Count}");
            }

            stopwatch.Stop();
            _setProgress(0);

            // Remove deleted files from groups
            foreach (var group in DuplicateGroups.ToList())
            {
                var toRemove = group.DuplicateFiles.Where(f => f.IsSelected &&
                    !System.IO.File.Exists(f.FilePath)).ToList();

                foreach (var file in toRemove)
                {
                    group.DuplicateFiles.Remove(file);
                    group.Files.Remove(file.FilePath);
                }

                group.FileCount = group.DuplicateFiles.Count;

                // Remove groups with only 1 file left (no longer duplicates)
                if (group.DuplicateFiles.Count <= 1)
                {
                    DuplicateGroups.Remove(group);
                }
                else
                {
                    // Recalculate wasted space
                    group.WastedSpace = group.FileSize * (group.FileCount - 1);
                }
            }

            // Update statistics
            DuplicateGroupsFound = DuplicateGroups.Count;
            TotalDuplicateFiles = DuplicateGroups.Sum(g => g.FileCount - 1);
            WastedSpaceGB = DuplicateGroups.Sum(g => g.WastedSpace) / (1024.0 * 1024.0 * 1024.0);
            UpdateSelectionStatistics();

            // Show results
            _notify.SetStatus($"Deletion complete! Deleted {deleted} files, freed {spaceGB:F2} GB");

            var message = $"Deleted: {deleted} files  |  Failed: {failed} files  |  Space Freed: {spaceGB:F2} GB  |  Duration: {_formatDuration(stopwatch.Elapsed)}";

            if (failed > 0 && failedFiles.Count <= 3)
            {
                message += "\n\nFailed: " + string.Join(", ", failedFiles.Select(f => System.IO.Path.GetFileName(f)));
            }
            else if (failed > 0)
            {
                message += $"\n\n{failed} files failed (check permissions)";
            }

            _notify.ShowCompletionBanner("Deletion", message, "🗑️");

            // Add to history
            _history.AddEntry("Delete Duplicates", deleted + failed, deleted,
                failed == 0 ? "Success" : $"Partial ({deleted}/{deleted + failed})");
        }

        private void MoveSelectedDuplicates()
        {
            var selectedFiles = DuplicateGroups
                .SelectMany(g => g.DuplicateFiles)
                .Where(f => f.IsSelected)
                .ToList();

            if (selectedFiles.Count == 0)
            {
                System.Windows.MessageBox.Show("No files selected.",
                    "No Selection", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            // Select destination folder
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to move duplicates to:",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var destinationFolder = dialog.SelectedPath;

            // Move files
            _notify.SetStatus("Moving duplicate files...");
            int moved = 0;
            int failed = 0;

            foreach (var file in selectedFiles)
            {
                try
                {
                    var fileName = System.IO.Path.GetFileName(file.FilePath);
                    var destPath = System.IO.Path.Combine(destinationFolder, fileName);

                    // Handle name conflicts
                    int counter = 1;
                    while (System.IO.File.Exists(destPath))
                    {
                        var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                        var ext = System.IO.Path.GetExtension(fileName);
                        destPath = System.IO.Path.Combine(destinationFolder, $"{nameWithoutExt} ({counter}){ext}");
                        counter++;
                    }

                    System.IO.File.Move(file.FilePath, destPath);
                    moved++;
                }
                catch
                {
                    failed++;
                }

                _setProgress(((moved + failed) / (double)selectedFiles.Count) * 100);
            }

            _setProgress(0);
            _notify.SetStatus($"Move complete! Moved {moved} files");

            System.Windows.MessageBox.Show(
                $"Move Complete!\n\n" +
                $"Moved: {moved} files\n" +
                $"Failed: {failed} files\n" +
                $"Destination: {destinationFolder}",
                "Move Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            // Refresh duplicate groups
            DetectDuplicates();
        }

        private void ExportDuplicateList()
        {
            if (DuplicateGroups.Count == 0)
            {
                System.Windows.MessageBox.Show("No duplicate groups to export.",
                    "No Data", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"Duplicates_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = "csv"
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            try
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Group,Hash,FilePath,FileSize,Created,Modified,Selected,Recommended");

                int groupNum = 1;
                foreach (var group in DuplicateGroups)
                {
                    foreach (var file in group.DuplicateFiles)
                    {
                        csv.AppendLine($"{groupNum}," +
                            $"\"{group.Hash}\"," +
                            $"\"{file.FilePath}\"," +
                            $"{file.FileSize}," +
                            $"{file.CreatedDate:yyyy-MM-dd HH:mm:ss}," +
                            $"{file.ModifiedDate:yyyy-MM-dd HH:mm:ss}," +
                            $"{file.IsSelected}," +
                            $"{file.IsRecommendedKeep}");
                    }
                    groupNum++;
                }

                System.IO.File.WriteAllText(dialog.FileName, csv.ToString());

                System.Windows.MessageBox.Show(
                    $"Successfully exported {DuplicateGroups.Count} duplicate groups to CSV!\n\n" +
                    $"File: {dialog.FileName}",
                    "Export Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error exporting list:\n{ex.Message}",
                    "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ClearDuplicateSelection()
        {
            foreach (var group in DuplicateGroups)
            {
                foreach (var file in group.DuplicateFiles)
                {
                    file.IsSelected = false;
                    file.IsRecommendedKeep = false;
                }
            }

            KeepStrategy = "None";
            UpdateSelectionStatistics();
        }

        private void ToggleGroupExpand(DuplicateGroup group)
        {
            if (group != null)
            {
                group.IsExpanded = !group.IsExpanded;
            }
        }
    }
}
