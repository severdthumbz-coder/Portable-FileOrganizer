using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using FileOrganizer.Commands;
using FileOrganizer.Models;
using FileOrganizer.Services;

namespace FileOrganizer.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, INotificationService, ITransferSettingsProvider, IOperationsSettingsProvider
    {
        #region Fields
        
        // Services
        private readonly Services.FileScanner _fileScanner;
        private readonly Services.ConfigManager _configManager;
        private readonly Services.HistoryManager _historyManager;
        private readonly Services.ResumeStateManager _resumeStateManager;
        private readonly Services.ToastNotificationService _toastService;
        private readonly NotificationService _notifications;
        private readonly SessionContext _session = new SessionContext();
        private IStatsSink _stats;
        
        private ScanMode _selectedScanMode = ScanMode.Auto;
        private CopyEngine _selectedCopyEngine = CopyEngine.CustomFast;
        private DestinationStructureMode _structureMode = DestinationStructureMode.PreserveStructure;
        private FileConflictResolution _conflictResolution = FileConflictResolution.Skip;
        
        private bool _enableDateOrganization = false;
        private string _dateFormat = "Year\\Month (2024\\02)";
        private bool _preserveTimestamps = true; // Default to preserving timestamps
        private VerificationMode _verificationMode = VerificationMode.Smart; // Default to Smart mode
        private bool _verifyExternalCopies = true; // Default ON: verify TeraCopy/FastCopy results
        
        private bool _continueOnErrors = true;
        private int _retryAttempts = 3;
        private int _retryDelaySeconds = 2;
        
        private string _statusMessage = "Ready";
        
        // Duplicate management
        
        // Verification statistics
        
        // Undo tracking
        
        // Resume state tracking
        
        // Space Analysis Results
        private bool _spaceAnalysisCompleted = false;
        private string _spaceAnalysisSourceFiles = string.Empty;
        private string _spaceAnalysisDestFreeSpace = string.Empty;
        private string _spaceAnalysisDiskUsage = string.Empty;
        private string _spaceAnalysisStatus = string.Empty;
        private bool _spaceAnalysisHasWarning = false;
        
        #endregion

        #region Properties
        
        // Scan Mode
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

        /// <summary>
        /// Gets description of scan mode based on detected system capabilities
        /// </summary>
        public string ScanModeDescription
        {
            get
            {
                var performanceManager = AdaptivePerformanceManager.Instance;
                return performanceManager.GetScanModeDescription(SelectedScanMode);
            }
        }

        /// <summary>
        /// Gets detected system capabilities description
        /// </summary>
        public string SystemDetectedDescription
        {
            get
            {
                var caps = AdaptivePerformanceManager.Instance.Capabilities;
                return caps.SystemDescription;
            }
        }

        // Storage Type Override
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

        public List<ScanModeItem> ScanModes { get; } = new List<ScanModeItem>
        {
            new ScanModeItem { Value = ScanMode.Auto, Display = "Auto" },
            new ScanModeItem { Value = ScanMode.Normal, Display = "Normal" },
            new ScanModeItem { Value = ScanMode.Fast, Display = "Fast" },
            new ScanModeItem { Value = ScanMode.Turbo, Display = "Turbo" }
        };

        // Copy Engine
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

        // Operation Mode
        // Backed by SessionContext (Build 1.4.3). The Live-button visibility (ShowLiveMoveButton /
        // ShowLiveCopyButton) now lives on OperationsViewModel, which subscribes to
        // SessionContext.OperationMode directly, so this setter no longer raises them.
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


        // Structure Mode
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

        // Conflict Resolution
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

        // Folders
        // Backed by SessionContext (Build 1.4.3). The capability-refresh side effect now
        // lives in SessionContext (Build 1.4.5) so a History Re-run triggers it too; this
        // setter just forwards and re-raises the dependent UI notifications.
        public string SourceFolder
        {
            get => _session.SourceFolder;
            set
            {
                if (_session.SourceFolder == value) return;
                _session.SourceFolder = value;
                OnPropertyChanged();
            }
        }

        // Backed by SessionContext (Build 1.4.3).
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

        // Backed by SessionContext (Build 1.4.3).
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

        // Backed by SessionContext (Build 1.4.3) — same instance, so existing
        // Add/Remove calls and the DataGrid binding are unaffected.
        public ObservableCollection<string> SourceFolders => _session.SourceFolders;

        // Options
        public bool EnableDateOrganization
        {
            get => _enableDateOrganization;
            set => SetProperty(ref _enableDateOrganization, value);
        }

        public bool PreserveTimestamps
        {
            get => _preserveTimestamps;
            set => SetProperty(ref _preserveTimestamps, value);
        }

        public bool VerifyExternalCopies
        {
            get => _verifyExternalCopies;
            set => SetProperty(ref _verifyExternalCopies, value);
        }

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

        public bool ContinueOnErrors
        {
            get => _continueOnErrors;
            set => SetProperty(ref _continueOnErrors, value);
        }

        public int RetryAttempts
        {
            get => _retryAttempts;
            set => SetProperty(ref _retryAttempts, value);
        }

        public int RetryDelaySeconds
        {
            get => _retryDelaySeconds;
            set => SetProperty(ref _retryDelaySeconds, value);
        }

        // Status
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string VersionInfo => "v5.0 build 1.4.8";



        // ---- Duplicates tab (extracted to DuplicatesViewModel in Build 1.4.6) ----
        public DuplicatesViewModel DuplicatesVM { get; }

        // Space Analysis Results
        public bool SpaceAnalysisCompleted
        {
            get => _spaceAnalysisCompleted;
            set => SetProperty(ref _spaceAnalysisCompleted, value);
        }

        public string SpaceAnalysisSourceFiles
        {
            get => _spaceAnalysisSourceFiles;
            set => SetProperty(ref _spaceAnalysisSourceFiles, value);
        }

        public string SpaceAnalysisDestFreeSpace
        {
            get => _spaceAnalysisDestFreeSpace;
            set => SetProperty(ref _spaceAnalysisDestFreeSpace, value);
        }

        public string SpaceAnalysisDiskUsage
        {
            get => _spaceAnalysisDiskUsage;
            set => SetProperty(ref _spaceAnalysisDiskUsage, value);
        }

        public string SpaceAnalysisStatus
        {
            get => _spaceAnalysisStatus;
            set => SetProperty(ref _spaceAnalysisStatus, value);
        }

        public bool SpaceAnalysisHasWarning
        {
            get => _spaceAnalysisHasWarning;
            set => SetProperty(ref _spaceAnalysisHasWarning, value);
        }

        // Collections
        // ---- History tab (extracted to HistoryViewModel in Build 1.4.5) ----
        public HistoryViewModel HistoryVM { get; }
        // Operational code adds entries via AddHistoryEntry (below); config/persistence read this.
        public ObservableCollection<HistoryEntry> History => HistoryVM.History;
        // ---- Exceptions tab (extracted to ExceptionsViewModel in Build 1.4.4) ----
        public ExceptionsViewModel ExceptionsVM { get; }
        // The scan pipeline (ApplyExceptionFilters) and config save/load read this collection,
        // so expose the child VM's exact instance here.
        public ObservableCollection<ExceptionFilter> Exceptions => ExceptionsVM.Exceptions;

        // ---- Feature ViewModels (extracted in Build 1.4.2) ----
        // The Automation and Search tabs bind to these child ViewModels. Each owns its own
        // state and depends only on INotificationService / ITransferSettingsProvider, not on this class.
        public AutomationViewModel Automation { get; }
        public SearchViewModel Search { get; }

        // ---- INotificationService ----
        // Lets child ViewModels report to the user without knowing about this class.
        void INotificationService.SetStatus(string message) => StatusMessage = message;
        void INotificationService.ShowCompletionBanner(string operation, string statistics, string icon)
            => _notifications.ShowCompletionBanner(operation, statistics, icon);

        /// <summary>
        /// The shared folder / operation state. Exposed so views can bind to it directly
        /// (e.g. {Binding Session.SourceFolder}) as tabs are extracted in later steps.
        /// </summary>
        public SessionContext Session => _session;

        // ---- ITransferSettingsProvider ----
        // Exposes only the transfer settings the automation features must honour.
        // Folder state now comes from SessionContext.
        Config ITransferSettingsProvider.BuildAutomationConfig() => new Config
        {
            CopyEngine = CopyEngine.CustomFast, // automation uses the safe default engine
            PreserveTimestamps = PreserveTimestamps,
            VerificationMode = VerificationMode,
            VerifyExternalCopies = VerifyExternalCopies,
            RetryAttempts = RetryAttempts,
            RetryDelaySeconds = RetryDelaySeconds
        };

        // ---- Statistics tab (extracted to StatisticsViewModel in Build 1.4.7) ----
        // StatisticsViewModel is the IStatsSink; operational code writes via the _stats field.
        public StatisticsViewModel StatisticsVM { get; }

        // ---- Operations tab (extracted to OperationsViewModel in Build 1.4.8) ----
        public OperationsViewModel OperationsVM { get; }

        // ---- IOperationsSettingsProvider ----
        // Supplies the Configuration-tab settings the Operations pipeline reads, plus the
        // exception filter (which stays with the scan pipeline here).
        ScanMode IOperationsSettingsProvider.SelectedScanMode => SelectedScanMode;
        DestinationStructureMode IOperationsSettingsProvider.StructureMode => StructureMode;
        FileConflictResolution IOperationsSettingsProvider.ConflictResolution => ConflictResolution;
        List<QueueEntry> IOperationsSettingsProvider.ApplyExceptionFilters(List<QueueEntry> entries) => ApplyExceptionFilters(entries);

        /// <summary>
        /// Startup hook (called from App). Delegates to OperationsViewModel, which owns the
        /// resume/undo pipeline.
        /// </summary>
        public void CheckForIncompleteOperation() => OperationsVM.CheckForIncompleteOperation();

        // Queue counters




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
        public ICommand TestNotificationsCommand { get; }
        
        #endregion

        #region Constructor
        
        public MainViewModel()
        {
            // Initialize services
            _fileScanner = new Services.FileScanner();
            _configManager = new Services.ConfigManager();
            _historyManager = new Services.HistoryManager();
            _resumeStateManager = new Services.ResumeStateManager();
            _toastService = new Services.ToastNotificationService();

            // The notification service owns the banner control and writes the status bar.
            // Created before the child ViewModels, which receive it.
            _notifications = new NotificationService(msg => StatusMessage = msg);

            // When SourceFolder changes anywhere (Config tab, History Re-run), refresh the
            // storage-detection UI text. The capability refresh itself happens in SessionContext.
            _session.SourceFolderChanged += () =>
            {
                OnPropertyChanged(nameof(SystemDetectedDescription));
                OnPropertyChanged(nameof(ScanModeDescription));
            };

            // Feature ViewModels. They receive narrow interfaces (this object implements both)
            // rather than a reference to MainViewModel itself.
            Automation = new AutomationViewModel(this, this, _session);
            Search = new SearchViewModel(_session);
            ExceptionsVM = new ExceptionsViewModel(_notifications, _session);
            HistoryVM = new HistoryViewModel(_notifications, _session, _historyManager);
            StatisticsVM = new StatisticsViewModel(_notifications);
            _stats = StatisticsVM;
            OperationsVM = new OperationsViewModel(
                _notifications,
                _session,
                HistoryVM,
                StatisticsVM,
                _toastService,
                _fileScanner,
                _resumeStateManager,
                this);                       // IOperationsSettingsProvider
            DuplicatesVM = new DuplicatesViewModel(
                _notifications,
                HistoryVM,
                _session,
                StatisticsVM,                // IStatsSink
                _toastService,
                p => OperationsVM.ProgressValue = p,        // shared progress bar (owned by OperationsVM)
                OperationsViewModel.FormatDuration,
                d => OperationsVM.LastOperationDuration = d,
                () => SelectedScanMode);
            
            // Initialize commands
            BrowseSourceCommand = new RelayCommand(_ => BrowseSource());
            BrowseDestinationCommand = new RelayCommand(_ => BrowseDestination());
            AddSourceFolderCommand = new RelayCommand(_ => AddSourceFolder());
            RemoveSourceFolderCommand = new RelayCommand(RemoveSourceFolder);
            DetectEngineCommand = new RelayCommand(_ => DetectEngine());
            AnalyzeSpaceCommand = new RelayCommand(_ => AnalyzeSpace());
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            ClearConfigCommand = new RelayCommand(_ => ClearConfig());
            TestNotificationsCommand = new RelayCommand(_ => TestNotifications());

            // Load persisted configuration and history
            LoadPersistedData();
        }

        #endregion

        #region Command Methods
        
        private void BrowseSource()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select Source Folder";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SourceFolder = dialog.SelectedPath;
                StatusMessage = $"Source folder selected: {SourceFolder}";
            }
        }

        private void BrowseDestination()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select Destination Folder";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DestinationFolder = dialog.SelectedPath;
                StatusMessage = $"Destination folder selected: {DestinationFolder}";
            }
        }

        private void AddSourceFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Add Source Folder";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SourceFolders.Add(dialog.SelectedPath);
                StatusMessage = $"Added source folder: {dialog.SelectedPath}";
            }
        }

        private void RemoveSourceFolder(object parameter)
        {
            if (parameter is string folder)
            {
                SourceFolders.Remove(folder);
                StatusMessage = $"Removed source folder: {folder}";
            }
        }

        private void DetectEngine()
        {
            StatusMessage = $"Detecting {SelectedCopyEngine}...";
            
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
                    StatusMessage = $"{SelectedCopyEngine} detected successfully at {result.InstallPath}";
                }
                else
                {
                    EngineDetectionStatus = "❌ Not Found";
                    EngineStatusColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)); // Red
                    StatusMessage = $"{SelectedCopyEngine} not found. Please install or specify path.";
                }
            }
        }

        /// <summary>
        /// Set the banner notification control reference (called from MainWindow)
        /// </summary>
        /// <summary>
        /// Called by MainWindow once the banner control exists. Forwards it to the
        /// notification service, which now owns the control reference.
        /// </summary>
        public void SetBannerNotification(Controls.BannerNotification banner)
        {
            _notifications.AttachBanner(banner);
        }

        /// <summary>
        /// Show completion banner with statistics.
        /// Kept as a private helper so the 19 existing call sites are unchanged;
        /// the logic now lives in NotificationService.
        /// </summary>
        private void ShowCompletionBanner(string operation, string statistics, string icon = "✅")
        {
            _notifications.ShowCompletionBanner(operation, statistics, icon);
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

                StatusMessage = "Analyzing disk space...";

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
                StatusMessage = "Analysis complete!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error analyzing space: {ex.Message}";
                System.Windows.MessageBox.Show($"Error analyzing disk space:\n{ex.Message}", 
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void SaveConfig()
        {
            try
            {
                var config = new Config
                {
                    ScanMode = SelectedScanMode,
                    CopyEngine = SelectedCopyEngine,
                    OperationMode = OperationMode,
                    StructureMode = StructureMode,
                    ConflictResolution = ConflictResolution,
                    SourceFolder = SourceFolder,
                    DestinationFolder = DestinationFolder,
                    UseMultipleSources = UseMultipleSources,
                    EnableDateOrganization = EnableDateOrganization,
                    PreserveTimestamps = PreserveTimestamps,
                    VerificationMode = VerificationMode,
                    VerifyExternalCopies = VerifyExternalCopies,
                    ContinueOnErrors = ContinueOnErrors,
                    RetryAttempts = RetryAttempts,
                    RetryDelaySeconds = RetryDelaySeconds,
                    StorageOverride = _storageOverrideNVMe ? "NVMe" : _storageOverrideSSD ? "SSD" : _storageOverrideHDD ? "HDD" : "Auto"
                };

                config.SourceFolders.AddRange(SourceFolders);
                config.Exceptions.AddRange(Exceptions);

                // Automation (Tier 1) persistence — state now lives in the child ViewModel
                config.Rules.AddRange(Automation.Rules);
                config.WatchFolders.AddRange(Automation.WatchFolders);
                config.WatchIncludeSubfolders = Automation.WatchIncludeSubfolders;
                config.ScheduleEnabled = Automation.ScheduleEnabled;
                config.ScheduleIntervalMinutes = Automation.ScheduleIntervalMinutes;
                config.ScheduleRunOnStart = Automation.ScheduleRunOnStart;

                if (_configManager.SaveConfig(config))
                {
                    StatusMessage = $"Configuration saved to: {_configManager.GetConfigPath()}";
                    ShowCompletionBanner("Configuration Saved", 
                        $"All settings saved successfully  |  Location: {_configManager.GetConfigPath()}", 
                        "💾");
                }
                else
                {
                    StatusMessage = "Failed to save configuration";
                    ShowCompletionBanner("Save Failed", 
                        "Failed to save configuration. Check file permissions and try again.", 
                        "⚠️");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving config: {ex.Message}";
                ShowCompletionBanner("Save Error", 
                    $"Error saving configuration: {ex.Message}", 
                    "❌");
            }
        }

        private void ClearConfig()
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to clear the saved configuration?", 
                "Confirm Clear", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (_configManager.ClearConfig())
                {
                    StatusMessage = "Configuration cleared";
                    ShowCompletionBanner("Configuration Cleared", 
                        "All saved settings have been reset to defaults", 
                        "🗑️");
                }
                else
                {
                    StatusMessage = "Failed to clear configuration";
                    ShowCompletionBanner("Clear Failed", 
                        "Failed to clear configuration. Check file permissions.", 
                        "⚠️");
                }
            }
        }

        private List<QueueEntry> ApplyExceptionFilters(List<QueueEntry> entries)
        {
            if (Exceptions.Count == 0 || !Exceptions.Any(e => e.IsEnabled))
                return entries;

            var filtered = new List<QueueEntry>();

            foreach (var entry in entries)
            {
                bool shouldExclude = false;

                foreach (var exception in Exceptions.Where(e => e.IsEnabled))
                {
                    if (exception.Type == ExceptionType.Exclude)
                    {
                        // Full exclusion
                        if (exception.IsFolder)
                        {
                            // Check if file is within this folder
                            if (entry.SourcePath.StartsWith(exception.Path, StringComparison.OrdinalIgnoreCase))
                            {
                                shouldExclude = true;
                                break;
                            }
                        }
                        else
                        {
                            // Check if this is the specific file
                            if (string.Equals(entry.SourcePath, exception.Path, StringComparison.OrdinalIgnoreCase))
                            {
                            shouldExclude = true;
                                break;
                            }
                        }
                    }
                    else if (exception.Type == ExceptionType.Semi)
                    {
                        // Semi-Exclude: Don't exclude the file, but mark it for special handling
                        // The folder structure will be flattened - only category organization
                        if (exception.IsFolder)
                        {
                            // Check if file is within this folder
                            if (entry.SourcePath.StartsWith(exception.Path, StringComparison.OrdinalIgnoreCase))
                            {
                                // Mark as semi-excluded (will be organized by category only)
                                entry.IsSemiExcluded = true;
                            }
                        }
                        else
                        {
                            // File-level semi-exclude = same as exclude (doesn't make sense to semi-exclude a file)
                            if (string.Equals(entry.SourcePath, exception.Path, StringComparison.OrdinalIgnoreCase))
                            {
                                shouldExclude = true;
                                break;
                            }
                        }
                    }
                }

                if (!shouldExclude)
                {
                    filtered.Add(entry);
                }
            }

            return filtered;
        }

        /// <summary>
        /// Test if toast notifications are working
        /// </summary>
        private void TestNotifications()
        {
            bool success = _toastService.TestNotification();
            
            if (success)
            {
                StatusMessage = "Test notification sent! Check your Windows Action Center.";
                System.Windows.MessageBox.Show(
                    "Test notification sent!\n\n" +
                    "If you don't see it:\n" +
                    "1. Check Windows Action Center (bottom-right corner)\n" +
                    "2. Verify notifications are enabled in Windows Settings\n" +
                    "3. Make sure Focus Assist is not blocking notifications\n" +
                    "4. Check that 'Portable File Organizer' is allowed in notification settings",
                    "Test Notification",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = "Failed to send test notification. Toast notifications may not be supported.";
                System.Windows.MessageBox.Show(
                    "Failed to send test notification.\n\n" +
                    "Possible reasons:\n" +
                    "• Notifications are disabled in Windows Settings\n" +
                    "• Running on Windows version older than 1809\n" +
                    "• Focus Assist is blocking notifications\n" +
                    "• App doesn't have notification permissions\n\n" +
                    "The app will continue to work, but without toast notifications.",
                    "Notification Test Failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Adds a history entry for an operation. Kept as a thin forwarder so the 16
        /// operational call sites are unchanged; the logic lives in HistoryViewModel.
        /// </summary>
        private void AddHistoryEntry(string mode, int filesScanned, int successCount, string status, 
            int filesVerified = 0, int verificationPassed = 0, int verificationFailed = 0, int verificationRetried = 0)
        {
            HistoryVM.AddEntry(mode, filesScanned, successCount, status,
                filesVerified, verificationPassed, verificationFailed, verificationRetried);
        }

        private void LoadPersistedData()
        {
            try
            {
                // Load configuration
                var config = _configManager.LoadConfig();
                if (config != null)
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

                    // Load storage override
                    if (config.StorageOverride == "NVMe")
                        StorageOverrideNVMe = true;
                    else if (config.StorageOverride == "SSD")
                        StorageOverrideSSD = true;
                    else if (config.StorageOverride == "HDD")
                        StorageOverrideHDD = true;
                    else
                        StorageOverrideAuto = true;

                    // Load source folders
                    foreach (var folder in config.SourceFolders)
                    {
                        SourceFolders.Add(folder);
                    }

                    // Load exceptions
                    foreach (var exception in config.Exceptions)
                    {
                        Exceptions.Add(exception);
                    }

                    // Load automation (Tier 1) settings into the child ViewModel
                    if (config.Rules != null)
                        foreach (var rule in config.Rules) Automation.Rules.Add(rule);
                    if (config.WatchFolders != null)
                        foreach (var wf in config.WatchFolders) Automation.WatchFolders.Add(wf);
                    Automation.WatchIncludeSubfolders = config.WatchIncludeSubfolders;
                    Automation.ScheduleEnabled = config.ScheduleEnabled;
                    Automation.ScheduleIntervalMinutes = config.ScheduleIntervalMinutes > 0 ? config.ScheduleIntervalMinutes : 60;
                    Automation.ScheduleRunOnStart = config.ScheduleRunOnStart;

                    StatusMessage = "Configuration loaded";
                }

                // Load history
                // History load is owned by HistoryViewModel.
                HistoryVM.LoadPersisted();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading persisted data: {ex.Message}");
                StatusMessage = "Ready (using defaults)";
            }
        }

        #endregion

        #region INotifyPropertyChanged
        
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        #endregion
    }

    // Helper classes for dropdown items
    public class ScanModeItem
    {
        public ScanMode Value { get; set; }
        public string Display { get; set; }
    }

    public class CopyEngineItem
    {
        public CopyEngine Value { get; set; }
        public string Display { get; set; }
    }

    public class ConflictResolutionItem
    {
        public FileConflictResolution Value { get; set; }
        public string Display { get; set; }
    }
}
