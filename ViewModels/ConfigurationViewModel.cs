using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using FileOrganizer.Commands;
using FileOrganizer.Models;
using FileOrganizer.Services;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// The Configuration tab (Build 1.4.9 — the final extraction that completes the
    /// MainViewModel strangle). Owns every Configuration-tab setting, the enum-wrapper item
    /// lists bound to the combos, engine-detection state, space-analysis state, and the tab's
    /// own commands.
    ///
    /// Boundary (per handoff §10):
    ///  - Folder / operation-mode state stays in SessionContext; the folder commands here write
    ///    to the session (so a History Re-run and the Config tab share one source of truth).
    ///  - SaveConfig()/ClearConfig() are cross-tab orchestration and stay in MainViewModel; the
    ///    Save/Clear commands here forward through IConfigPersistence. The settings-block detail
    ///    lives here in BuildConfig()/ApplyConfig(), which MainViewModel's persistence calls.
    ///  - MainViewModel's IOperationsSettingsProvider / ITransferSettingsProvider impls read
    ///    these properties (re-pointed from MainViewModel.* to ConfigVM.*), so no downstream
    ///    construction changes.
    /// </summary>
    public class ConfigurationViewModel : ViewModelBase
    {
        private readonly SessionContext _session;
        private readonly INotificationService _notifications;
        private readonly IConfigPersistence _persistence;

        public ConfigurationViewModel(
            SessionContext session,
            INotificationService notifications,
            IConfigPersistence persistence)
        {
            _session = session;
            _notifications = notifications;
            _persistence = persistence;

            // When SourceFolder changes anywhere (this tab, a History Re-run), the
            // storage-detection description text depends on it. SessionContext raises the
            // event after refreshing capabilities; we re-raise the dependent UI notifications.
            _session.SourceFolderChanged += () =>
            {
                OnPropertyChanged(nameof(SystemDetectedDescription));
                OnPropertyChanged(nameof(ScanModeDescription));
            };

            BrowseSourceCommand = new RelayCommand(_ => BrowseSource());
            BrowseDestinationCommand = new RelayCommand(_ => BrowseDestination());
            AddSourceFolderCommand = new RelayCommand(_ => AddSourceFolder());
            RemoveSourceFolderCommand = new RelayCommand(RemoveSourceFolder);
            DetectEngineCommand = new RelayCommand(_ => DetectEngine());
            AnalyzeSpaceCommand = new RelayCommand(_ => AnalyzeSpace());
            SaveConfigCommand = new RelayCommand(_ => _persistence.SaveConfig());
            ClearConfigCommand = new RelayCommand(_ => _persistence.ClearConfig());
        }

        #region Scan Mode

        private ScanMode _selectedScanMode = ScanMode.Auto;
        public ScanMode SelectedScanMode
        {
            get => _selectedScanMode;
            set
            {
                if (SetProperty(ref _selectedScanMode, value))
                {
                    OnPropertyChanged(nameof(ScanModeDescription));
                }
            }
        }

        /// <summary>Description of scan mode based on detected system capabilities.</summary>
        public string ScanModeDescription
        {
            get
            {
                var performanceManager = AdaptivePerformanceManager.Instance;
                return performanceManager.GetScanModeDescription(SelectedScanMode);
            }
        }

        /// <summary>Detected system capabilities description.</summary>
        public string SystemDetectedDescription
        {
            get
            {
                var caps = AdaptivePerformanceManager.Instance.Capabilities;
                return caps.SystemDescription;
            }
        }

        public List<ScanModeItem> ScanModes { get; } = new List<ScanModeItem>
        {
            new ScanModeItem { Value = ScanMode.Auto, Display = "Auto" },
            new ScanModeItem { Value = ScanMode.Normal, Display = "Normal" },
            new ScanModeItem { Value = ScanMode.Fast, Display = "Fast" },
            new ScanModeItem { Value = ScanMode.Turbo, Display = "Turbo" }
        };

        #endregion

        #region Storage Type Override

        private bool _storageOverrideAuto = true;
        private bool _storageOverrideNVMe = false;
        private bool _storageOverrideSSD = false;
        private bool _storageOverrideHDD = false;

        public bool StorageOverrideAuto
        {
            get => _storageOverrideAuto;
            set
            {
                if (SetProperty(ref _storageOverrideAuto, value) && value)
                {
                    _storageOverrideNVMe = false;
                    _storageOverrideSSD = false;
                    _storageOverrideHDD = false;
                    OnPropertyChanged(nameof(StorageOverrideNVMe));
                    OnPropertyChanged(nameof(StorageOverrideSSD));
                    OnPropertyChanged(nameof(StorageOverrideHDD));
                    ApplyStorageOverride(null);
                }
            }
        }

        public bool StorageOverrideNVMe
        {
            get => _storageOverrideNVMe;
            set
            {
                if (SetProperty(ref _storageOverrideNVMe, value) && value)
                {
                    _storageOverrideAuto = false;
                    _storageOverrideSSD = false;
                    _storageOverrideHDD = false;
                    OnPropertyChanged(nameof(StorageOverrideAuto));
                    OnPropertyChanged(nameof(StorageOverrideSSD));
                    OnPropertyChanged(nameof(StorageOverrideHDD));
                    ApplyStorageOverride(StorageType.NVMe);
                }
            }
        }

        public bool StorageOverrideSSD
        {
            get => _storageOverrideSSD;
            set
            {
                if (SetProperty(ref _storageOverrideSSD, value) && value)
                {
                    _storageOverrideAuto = false;
                    _storageOverrideNVMe = false;
                    _storageOverrideHDD = false;
                    OnPropertyChanged(nameof(StorageOverrideAuto));
                    OnPropertyChanged(nameof(StorageOverrideNVMe));
                    OnPropertyChanged(nameof(StorageOverrideHDD));
                    ApplyStorageOverride(StorageType.SSD);
                }
            }
        }

        public bool StorageOverrideHDD
        {
            get => _storageOverrideHDD;
            set
            {
                if (SetProperty(ref _storageOverrideHDD, value) && value)
                {
                    _storageOverrideAuto = false;
                    _storageOverrideNVMe = false;
                    _storageOverrideSSD = false;
                    OnPropertyChanged(nameof(StorageOverrideAuto));
                    OnPropertyChanged(nameof(StorageOverrideNVMe));
                    OnPropertyChanged(nameof(StorageOverrideSSD));
                    ApplyStorageOverride(StorageType.HDD);
                }
            }
        }

        private void ApplyStorageOverride(StorageType? storageType)
        {
            AdaptivePerformanceManager.Instance.SetStorageOverride(storageType);
            OnPropertyChanged(nameof(SystemDetectedDescription));
            OnPropertyChanged(nameof(ScanModeDescription));
        }

        #endregion

        #region Copy Engine

        private CopyEngine _selectedCopyEngine = CopyEngine.CustomFast;
        public CopyEngine SelectedCopyEngine
        {
            get => _selectedCopyEngine;
            set
            {
                if (SetProperty(ref _selectedCopyEngine, value))
                {
                    OnPropertyChanged(nameof(CopyEngineDescription));
                    OnPropertyChanged(nameof(ShowEngineDetection));
                    OnPropertyChanged(nameof(SelectedEngineLabel));

                    // Reset detection status when engine changes
                    EngineDetectionStatus = "Not Detected";
                    EngineStatusColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158)); // Gray
                }
            }
        }

        public string CopyEngineDescription
        {
            get
            {
                return SelectedCopyEngine switch
                {
                    CopyEngine.WindowsStandard => "Windows built-in copy API. Reliable but slower. No per-file progress reporting.",
                    CopyEngine.CustomFast => "Multi-threaded, buffered I/O with real-time progress. Recommended for best performance.",
                    CopyEngine.TeraCopy => "Uses TeraCopy if installed. Excellent for large files with verification. Requires TeraCopy to be installed.",
                    CopyEngine.FastCopy => "Uses FastCopy if installed. Extremely fast for large operations. Requires FastCopy to be installed.",
                    _ => string.Empty
                };
            }
        }

        public bool ShowEngineDetection => SelectedCopyEngine == CopyEngine.TeraCopy || SelectedCopyEngine == CopyEngine.FastCopy;

        public string SelectedEngineLabel => SelectedCopyEngine == CopyEngine.TeraCopy ? "TeraCopy:" : "FastCopy:";

        private string _engineDetectionStatus = "❌ Not Found";
        public string EngineDetectionStatus
        {
            get => _engineDetectionStatus;
            set => SetProperty(ref _engineDetectionStatus, value);
        }

        private SolidColorBrush _engineStatusColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)); // Red
        public SolidColorBrush EngineStatusColor
        {
            get => _engineStatusColor;
            set => SetProperty(ref _engineStatusColor, value);
        }

        public List<CopyEngineItem> CopyEngines { get; } = new List<CopyEngineItem>
        {
            new CopyEngineItem { Value = CopyEngine.WindowsStandard, Display = "Windows Standard" },
            new CopyEngineItem { Value = CopyEngine.CustomFast, Display = "Custom Fast (Recommended)" },
            new CopyEngineItem { Value = CopyEngine.TeraCopy, Display = "TeraCopy" },
            new CopyEngineItem { Value = CopyEngine.FastCopy, Display = "FastCopy" }
        };

        #endregion

        #region Operation Mode / Folders (session-backed)

        // Operation mode and folders live in SessionContext (Build 1.4.3); these forward so the
        // Config view can bind them on ConfigVM exactly as it did on MainViewModel.
        public FileOperationMode OperationMode
        {
            get => _session.OperationMode;
            set
            {
                if (_session.OperationMode == value) return;
                _session.OperationMode = value;
                OnPropertyChanged();
            }
        }

        public string SourceFolder
        {
            get => _session.SourceFolder;
            set
            {
                if (_session.SourceFolder == value) return;
                _session.SourceFolder = value; // capability refresh side effect lives in SessionContext
                OnPropertyChanged();
            }
        }

        public string DestinationFolder
        {
            get => _session.DestinationFolder;
            set
            {
                if (_session.DestinationFolder == value) return;
                _session.DestinationFolder = value;
                OnPropertyChanged();
            }
        }

        public bool UseMultipleSources
        {
            get => _session.UseMultipleSources;
            set
            {
                if (_session.UseMultipleSources == value) return;
                _session.UseMultipleSources = value;
                OnPropertyChanged();
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<string> SourceFolders => _session.SourceFolders;

        /// <summary>
        /// Write-only status passthrough for the SourceFolders removal code-behind handler,
        /// which (like the Exceptions tab) sets a status line directly. Routes to the shared
        /// notification service so the status bar updates exactly as before.
        /// </summary>
        public string StatusMessage
        {
            set => _notifications.SetStatus(value);
        }

        #endregion

        #region Structure Mode

        private DestinationStructureMode _structureMode = DestinationStructureMode.PreserveStructure;
        public DestinationStructureMode StructureMode
        {
            get => _structureMode;
            set
            {
                if (SetProperty(ref _structureMode, value))
                {
                    OnPropertyChanged(nameof(StructureModeDescription));
                }
            }
        }

        public string StructureModeDescription
        {
            get
            {
                return StructureMode switch
                {
                    DestinationStructureMode.OrganizeByCategory => "Files sorted into type-based subfolders (Documents, Images, Videos, etc.). Source structure not preserved.",
                    DestinationStructureMode.PreserveStructure => "Maintains exact source folder hierarchy at destination. Recommended for preserving organization.",
                    DestinationStructureMode.Hybrid => "Organizes by category first, then preserves relative subfolder structure within each category.",
                    _ => string.Empty
                };
            }
        }

        #endregion

        #region Conflict Resolution

        private FileConflictResolution _conflictResolution = FileConflictResolution.Skip;
        public FileConflictResolution ConflictResolution
        {
            get => _conflictResolution;
            set
            {
                if (SetProperty(ref _conflictResolution, value))
                {
                    OnPropertyChanged(nameof(ConflictResolutionDescription));
                }
            }
        }

        public string ConflictResolutionDescription
        {
            get
            {
                return ConflictResolution switch
                {
                    FileConflictResolution.Skip => "Skips the file if it already exists at the destination. Original file remains unchanged.",
                    FileConflictResolution.Overwrite => "Replaces the existing file at the destination with the source file.",
                    FileConflictResolution.OverwriteIfNewer => "Replaces the existing file only if the source file is newer (based on modified date).",
                    FileConflictResolution.RenameKeepBoth => "Renames the source file (adds suffix) and keeps both files at the destination.",
                    _ => string.Empty
                };
            }
        }

        public List<ConflictResolutionItem> ConflictResolutions { get; } = new List<ConflictResolutionItem>
        {
            new ConflictResolutionItem { Value = FileConflictResolution.Skip, Display = "Skip" },
            new ConflictResolutionItem { Value = FileConflictResolution.Overwrite, Display = "Overwrite" },
            new ConflictResolutionItem { Value = FileConflictResolution.OverwriteIfNewer, Display = "Overwrite if Newer" },
            new ConflictResolutionItem { Value = FileConflictResolution.RenameKeepBoth, Display = "Rename (Keep Both)" }
        };

        #endregion

        #region Options

        private bool _enableDateOrganization = false;
        public bool EnableDateOrganization
        {
            get => _enableDateOrganization;
            set => SetProperty(ref _enableDateOrganization, value);
        }

        private bool _preserveTimestamps = true;
        public bool PreserveTimestamps
        {
            get => _preserveTimestamps;
            set => SetProperty(ref _preserveTimestamps, value);
        }

        private bool _verifyExternalCopies = true;
        public bool VerifyExternalCopies
        {
            get => _verifyExternalCopies;
            set => SetProperty(ref _verifyExternalCopies, value);
        }

        private VerificationMode _verificationMode = VerificationMode.Smart;
        public VerificationMode VerificationMode
        {
            get => _verificationMode;
            set => SetProperty(ref _verificationMode, value);
        }

        public List<VerificationMode> VerificationModes { get; } = new List<VerificationMode>
        {
            VerificationMode.None,
            VerificationMode.SizeOnly,
            VerificationMode.Smart,
            VerificationMode.FullHash
        };

        private string _dateFormat = "Year\\Month (2024\\02)";
        public string DateFormat
        {
            get => _dateFormat;
            set => SetProperty(ref _dateFormat, value);
        }

        public List<string> DateFormats { get; } = new List<string>
        {
            "Year\\Month (2024\\02)",
            "Year\\Quarter (2024\\Q1)",
            "Year\\Month Name (2024\\February)",
            "Year\\Week (2024\\Week-08)"
        };

        private bool _continueOnErrors = true;
        public bool ContinueOnErrors
        {
            get => _continueOnErrors;
            set => SetProperty(ref _continueOnErrors, value);
        }

        private int _retryAttempts = 3;
        public int RetryAttempts
        {
            get => _retryAttempts;
            set => SetProperty(ref _retryAttempts, value);
        }

        private int _retryDelaySeconds = 2;
        public int RetryDelaySeconds
        {
            get => _retryDelaySeconds;
            set => SetProperty(ref _retryDelaySeconds, value);
        }

        #endregion

        #region Space Analysis

        private bool _spaceAnalysisCompleted = false;
        public bool SpaceAnalysisCompleted
        {
            get => _spaceAnalysisCompleted;
            set => SetProperty(ref _spaceAnalysisCompleted, value);
        }

        private string _spaceAnalysisSourceFiles = string.Empty;
        public string SpaceAnalysisSourceFiles
        {
            get => _spaceAnalysisSourceFiles;
            set => SetProperty(ref _spaceAnalysisSourceFiles, value);
        }

        private string _spaceAnalysisDestFreeSpace = string.Empty;
        public string SpaceAnalysisDestFreeSpace
        {
            get => _spaceAnalysisDestFreeSpace;
            set => SetProperty(ref _spaceAnalysisDestFreeSpace, value);
        }

        private string _spaceAnalysisDiskUsage = string.Empty;
        public string SpaceAnalysisDiskUsage
        {
            get => _spaceAnalysisDiskUsage;
            set => SetProperty(ref _spaceAnalysisDiskUsage, value);
        }

        private string _spaceAnalysisStatus = string.Empty;
        public string SpaceAnalysisStatus
        {
            get => _spaceAnalysisStatus;
            set => SetProperty(ref _spaceAnalysisStatus, value);
        }

        private bool _spaceAnalysisHasWarning = false;
        public bool SpaceAnalysisHasWarning
        {
            get => _spaceAnalysisHasWarning;
            set => SetProperty(ref _spaceAnalysisHasWarning, value);
        }

        #endregion

        #region Commands

        public ICommand BrowseSourceCommand { get; }
        public ICommand BrowseDestinationCommand { get; }
        public ICommand AddSourceFolderCommand { get; }
        public ICommand RemoveSourceFolderCommand { get; }
        public ICommand DetectEngineCommand { get; }
        public ICommand AnalyzeSpaceCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand ClearConfigCommand { get; }

        #endregion

        #region Command Methods

        private void BrowseSource()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select Source Folder";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SourceFolder = dialog.SelectedPath;
                _notifications.SetStatus($"Source folder selected: {SourceFolder}");
            }
        }

        private void BrowseDestination()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select Destination Folder";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DestinationFolder = dialog.SelectedPath;
                _notifications.SetStatus($"Destination folder selected: {DestinationFolder}");
            }
        }

        private void AddSourceFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Add Source Folder";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SourceFolders.Add(dialog.SelectedPath);
                _notifications.SetStatus($"Added source folder: {dialog.SelectedPath}");
            }
        }

        private void RemoveSourceFolder(object parameter)
        {
            if (parameter is string folder)
            {
                SourceFolders.Remove(folder);
                _notifications.SetStatus($"Removed source folder: {folder}");
            }
        }

        private void DetectEngine()
        {
            _notifications.SetStatus($"Detecting {SelectedCopyEngine}...");

            Services.EngineDetector.DetectionResult result = null;

            if (SelectedCopyEngine == CopyEngine.TeraCopy)
            {
                result = Services.EngineDetector.DetectTeraCopy();
            }
            else if (SelectedCopyEngine == CopyEngine.FastCopy)
            {
                result = Services.EngineDetector.DetectFastCopy();
            }

            if (result != null)
            {
                if (result.IsInstalled)
                {
                    EngineDetectionStatus = "✓ Detected";
                    EngineStatusColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // Green
                    _notifications.SetStatus($"{SelectedCopyEngine} detected successfully at {result.InstallPath}");
                }
                else
                {
                    EngineDetectionStatus = "❌ Not Found";
                    EngineStatusColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)); // Red
                    _notifications.SetStatus($"{SelectedCopyEngine} not found. Please install or specify path.");
                }
            }
        }

        private void AnalyzeSpace()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SourceFolder) || !System.IO.Directory.Exists(SourceFolder))
                {
                    System.Windows.MessageBox.Show("Please select a valid source folder first.",
                        "Source Folder Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(DestinationFolder) || !System.IO.Directory.Exists(DestinationFolder))
                {
                    System.Windows.MessageBox.Show("Please select a valid destination folder first.",
                        "Destination Folder Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                _notifications.SetStatus("Analyzing disk space...");

                // Get source drive info
                var sourceDrive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(SourceFolder));
                var destDrive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(DestinationFolder));

                // Calculate source folder size and count files
                var sourceDir = new System.IO.DirectoryInfo(SourceFolder);
                long estimatedSize = 0;
                int fileCount = 0;

                try
                {
                    var files = sourceDir.EnumerateFiles("*", System.IO.SearchOption.AllDirectories).ToList();
                    fileCount = files.Count;
                    estimatedSize = files.Sum(fi => fi.Length);
                }
                catch
                {
                    // If we can't enumerate, set to 0
                    fileCount = 0;
                    estimatedSize = 0;
                }

                double sourceSizeGB = estimatedSize / (1024.0 * 1024.0 * 1024.0);
                double destFreeGB = destDrive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                double destTotalGB = destDrive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                double destUsedGB = destTotalGB - destFreeGB;
                double diskUsagePercent = (destUsedGB / destTotalGB) * 100.0;

                // Update properties for UI
                SpaceAnalysisSourceFiles = $"{fileCount} files ({sourceSizeGB:F2} GB)";
                SpaceAnalysisDestFreeSpace = $"{destFreeGB:F2} GB / {destTotalGB:F2} GB";
                SpaceAnalysisDiskUsage = $"{diskUsagePercent:F1}%";

                if (destFreeGB < sourceSizeGB)
                {
                    SpaceAnalysisStatus = "⚠ Insufficient space!";
                    SpaceAnalysisHasWarning = true;
                }
                else
                {
                    SpaceAnalysisStatus = "✓ Sufficient space available";
                    SpaceAnalysisHasWarning = false;
                }

                SpaceAnalysisCompleted = true;
                _notifications.SetStatus("Analysis complete!");
            }
            catch (Exception ex)
            {
                _notifications.SetStatus($"Error analyzing space: {ex.Message}");
                System.Windows.MessageBox.Show($"Error analyzing disk space:\n{ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        #endregion

        #region Persistence (settings block only; orchestration stays in MainViewModel)

        /// <summary>
        /// Populates the Configuration-settings portion of a Config being saved. MainViewModel's
        /// SaveConfig() calls this, then adds the cross-tab pieces (SourceFolders, Exceptions,
        /// Automation) and writes the file.
        /// </summary>
        public void BuildConfig(Config config)
        {
            config.ScanMode = SelectedScanMode;
            config.CopyEngine = SelectedCopyEngine;
            config.OperationMode = OperationMode;
            config.StructureMode = StructureMode;
            config.ConflictResolution = ConflictResolution;
            config.SourceFolder = SourceFolder;
            config.DestinationFolder = DestinationFolder;
            config.UseMultipleSources = UseMultipleSources;
            config.EnableDateOrganization = EnableDateOrganization;
            config.PreserveTimestamps = PreserveTimestamps;
            config.VerificationMode = VerificationMode;
            config.VerifyExternalCopies = VerifyExternalCopies;
            config.ContinueOnErrors = ContinueOnErrors;
            config.RetryAttempts = RetryAttempts;
            config.RetryDelaySeconds = RetryDelaySeconds;
            config.StorageOverride = _storageOverrideNVMe ? "NVMe" : _storageOverrideSSD ? "SSD" : _storageOverrideHDD ? "HDD" : "Auto";
        }

        /// <summary>
        /// Loads the Configuration-settings portion from a persisted Config. MainViewModel's
        /// LoadPersistedData() calls this, then loads the cross-tab pieces itself.
        /// </summary>
        public void ApplyConfig(Config config)
        {
            SelectedScanMode = config.ScanMode;
            SelectedCopyEngine = config.CopyEngine;
            OperationMode = config.OperationMode;
            StructureMode = config.StructureMode;
            ConflictResolution = config.ConflictResolution;
            SourceFolder = config.SourceFolder ?? string.Empty;
            DestinationFolder = config.DestinationFolder ?? string.Empty;
            UseMultipleSources = config.UseMultipleSources;
            EnableDateOrganization = config.EnableDateOrganization;
            PreserveTimestamps = config.PreserveTimestamps;
            VerificationMode = config.VerificationMode;
            VerifyExternalCopies = config.VerifyExternalCopies;
            ContinueOnErrors = config.ContinueOnErrors;
            RetryAttempts = config.RetryAttempts;
            RetryDelaySeconds = config.RetryDelaySeconds;

            // Storage override (radios clear each other via their setters)
            if (config.StorageOverride == "NVMe")
                StorageOverrideNVMe = true;
            else if (config.StorageOverride == "SSD")
                StorageOverrideSSD = true;
            else if (config.StorageOverride == "HDD")
                StorageOverrideHDD = true;
            else
                StorageOverrideAuto = true;
        }

        #endregion
    }
}
