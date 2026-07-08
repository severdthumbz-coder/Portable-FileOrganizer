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
    public class MainViewModel : INotifyPropertyChanged
    {
        #region Fields
        
        // Services
        private readonly Services.FileScanner _fileScanner;
        private readonly Services.ConfigManager _configManager;
        private readonly Services.HistoryManager _historyManager;
        private readonly Services.ResumeStateManager _resumeStateManager;
        private readonly Services.ToastNotificationService _toastService;
        private Controls.BannerNotification _bannerNotification;
        
        private ScanMode _selectedScanMode = ScanMode.Auto;
        private CopyEngine _selectedCopyEngine = CopyEngine.CustomFast;
        private FileOperationMode _operationMode = FileOperationMode.Move;
        private DestinationStructureMode _structureMode = DestinationStructureMode.PreserveStructure;
        private FileConflictResolution _conflictResolution = FileConflictResolution.Skip;
        
        private string _sourceFolder = string.Empty;
        private string _destinationFolder = string.Empty;
        private bool _useMultipleSources = false;
        private bool _enableDateOrganization = false;
        private string _dateFormat = "Year\\Month (2024\\02)";
        private bool _preserveTimestamps = true; // Default to preserving timestamps
        private VerificationMode _verificationMode = VerificationMode.Smart; // Default to Smart mode
        private bool _verifyExternalCopies = true; // Default ON: verify TeraCopy/FastCopy results
        
        private bool _continueOnErrors = true;
        private int _retryAttempts = 3;
        private int _retryDelaySeconds = 2;
        
        private string _statusMessage = "Ready";
        private double _progressValue = 0;
        private int _totalFilesOrganized = 0;
        private int _totalOperations = 0;
        private double _dataProcessedGB = 0.0;
        private int _duplicateGroupsFound = 0;
        private double _wastedSpaceGB = 0.0;
        
        // Duplicate management
        private ObservableCollection<Services.DuplicateGroup> _duplicateGroups = new ObservableCollection<Services.DuplicateGroup>();
        private string _keepStrategy = "None";
        private bool _useQuickScan = false;
        private int _totalDuplicateFiles = 0;
        private int _selectedForDeletion = 0;
        private double _selectedDeletionSpaceGB = 0.0;
        
        // Verification statistics
        private int _totalFilesVerified = 0;
        private int _verificationPassed = 0;
        private int _verificationFailed = 0;
        private int _verificationRetried = 0;
        private List<VerificationLog> _verificationLogs = new List<VerificationLog>();
        
        // Undo tracking
        private List<QueueEntry> _lastMoveOperation = new List<QueueEntry>();
        
        // Resume state tracking
        private ResumeState _currentResumeState = null;
        private int _filesProcessedSinceLastSave = 0;
        private const int FilesPerStateSave = 10; // Save state every 10 files
        
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
        public FileOperationMode OperationMode
        {
            get => _operationMode;
            set
            {
                if (SetProperty(ref _operationMode, value))
                {
                    OnPropertyChanged(nameof(ShowLiveMoveButton));
                    OnPropertyChanged(nameof(ShowLiveCopyButton));
                }
            }
        }

        public bool ShowLiveMoveButton => OperationMode == FileOperationMode.Move;
        public bool ShowLiveCopyButton => OperationMode == FileOperationMode.Copy;

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
        public string SourceFolder
        {
            get => _sourceFolder;
            set
            {
                if (SetProperty(ref _sourceFolder, value))
                {
                    // Refresh system capabilities with new source path
                    // This will detect actual storage type (HDD/SSD/NVMe)
                    if (!string.IsNullOrEmpty(value) && System.IO.Directory.Exists(value))
                    {
                        AdaptivePerformanceManager.Instance.RefreshCapabilities(value);
                        
                        // Update UI to show new storage detection
                        OnPropertyChanged(nameof(SystemDetectedDescription));
                        OnPropertyChanged(nameof(ScanModeDescription));
                    }
                }
            }
        }

        public string DestinationFolder
        {
            get => _destinationFolder;
            set => SetProperty(ref _destinationFolder, value);
        }

        public bool UseMultipleSources
        {
            get => _useMultipleSources;
            set => SetProperty(ref _useMultipleSources, value);
        }

        public ObservableCollection<string> SourceFolders { get; } = new ObservableCollection<string>();

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

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public string VersionInfo => "v5.0 build 1.3.0";

        // Item sources for the Automation tab combos
        public List<RuleConditionType> RuleConditionTypes { get; } =
            System.Enum.GetValues(typeof(RuleConditionType)).Cast<RuleConditionType>().ToList();
        public List<RuleMatchMode> RuleMatchModes { get; } =
            System.Enum.GetValues(typeof(RuleMatchMode)).Cast<RuleMatchMode>().ToList();


        private string _lastOperationDuration = "";
        public string LastOperationDuration
        {
            get => _lastOperationDuration;
            set => SetProperty(ref _lastOperationDuration, value);
        }

        // Statistics
        public int TotalFilesOrganized
        {
            get => _totalFilesOrganized;
            set => SetProperty(ref _totalFilesOrganized, value);
        }

        public int TotalOperations
        {
            get => _totalOperations;
            set => SetProperty(ref _totalOperations, value);
        }

        public double DataProcessedGB
        {
            get => _dataProcessedGB;
            set => SetProperty(ref _dataProcessedGB, value);
        }

        public int DuplicateGroupsFound
        {
            get => _duplicateGroupsFound;
            set => SetProperty(ref _duplicateGroupsFound, value);
        }

        public double WastedSpaceGB
        {
            get => _wastedSpaceGB;
            set => SetProperty(ref _wastedSpaceGB, value);
        }
        
        // Duplicate Management
        public ObservableCollection<Services.DuplicateGroup> DuplicateGroups
        {
            get => _duplicateGroups;
            set => SetProperty(ref _duplicateGroups, value);
        }
        
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
        
        public bool UseQuickScan
        {
            get => _useQuickScan;
            set => SetProperty(ref _useQuickScan, value);
        }
        
        public int TotalDuplicateFiles
        {
            get => _totalDuplicateFiles;
            set => SetProperty(ref _totalDuplicateFiles, value);
        }
        
        public int SelectedForDeletion
        {
            get => _selectedForDeletion;
            set => SetProperty(ref _selectedForDeletion, value);
        }
        
        public double SelectedDeletionSpaceGB
        {
            get => _selectedDeletionSpaceGB;
            set => SetProperty(ref _selectedDeletionSpaceGB, value);
        }

        // Verification Statistics
        public int TotalFilesVerified
        {
            get => _totalFilesVerified;
            set => SetProperty(ref _totalFilesVerified, value);
        }

        public int VerificationPassed
        {
            get => _verificationPassed;
            set => SetProperty(ref _verificationPassed, value);
        }

        public int VerificationFailed
        {
            get => _verificationFailed;
            set => SetProperty(ref _verificationFailed, value);
        }

        public int VerificationRetried
        {
            get => _verificationRetried;
            set => SetProperty(ref _verificationRetried, value);
        }

        public double VerificationSuccessRate
        {
            get => TotalFilesVerified > 0 ? (double)VerificationPassed / TotalFilesVerified * 100 : 0;
        }

        public List<VerificationLog> VerificationLogs => _verificationLogs;

        public List<VerificationLog> RecentVerificationFailures => 
            _verificationLogs.Where(x => !x.Passed).OrderByDescending(x => x.Timestamp).Take(10).ToList();

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
        public ObservableCollection<QueueEntry> FileQueue { get; } = new ObservableCollection<QueueEntry>();
        public ObservableCollection<HistoryEntry> History { get; } = new ObservableCollection<HistoryEntry>();
        public ObservableCollection<ExceptionFilter> Exceptions { get; } = new ObservableCollection<ExceptionFilter>();

        // ---- Automation (Tier 1) ----
        public ObservableCollection<OrganizationRule> Rules { get; } = new ObservableCollection<OrganizationRule>();
        public ObservableCollection<string> WatchFolders { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AutomationLog { get; } = new ObservableCollection<string>();

        private FolderWatcherService _folderWatcher;
        private ScheduledSortService _scheduler;

        private bool _watchIncludeSubfolders;
        public bool WatchIncludeSubfolders
        {
            get => _watchIncludeSubfolders;
            set => SetProperty(ref _watchIncludeSubfolders, value);
        }

        private bool _isWatching;
        public bool IsWatching
        {
            get => _isWatching;
            set => SetProperty(ref _isWatching, value);
        }

        private bool _scheduleEnabled;
        public bool ScheduleEnabled
        {
            get => _scheduleEnabled;
            set => SetProperty(ref _scheduleEnabled, value);
        }

        private int _scheduleIntervalMinutes = 60;
        public int ScheduleIntervalMinutes
        {
            get => _scheduleIntervalMinutes;
            set => SetProperty(ref _scheduleIntervalMinutes, value);
        }

        private bool _scheduleRunOnStart;
        public bool ScheduleRunOnStart
        {
            get => _scheduleRunOnStart;
            set => SetProperty(ref _scheduleRunOnStart, value);
        }

        private bool _isScheduleRunning;
        public bool IsScheduleRunning
        {
            get => _isScheduleRunning;
            set => SetProperty(ref _isScheduleRunning, value);
        }

        private OrganizationRule _selectedRule;
        public OrganizationRule SelectedRule
        {
            get => _selectedRule;
            set => SetProperty(ref _selectedRule, value);
        }

        // Queue counters
        private int _pendingCount = 0;
        private int _movedCount = 0;
        private int _failedCount = 0;

        public int PendingCount
        {
            get => _pendingCount;
            set => SetProperty(ref _pendingCount, value);
        }

        public int MovedCount
        {
            get => _movedCount;
            set => SetProperty(ref _movedCount, value);
        }

        public int FailedCount
        {
            get => _failedCount;
            set => SetProperty(ref _failedCount, value);
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
        public ICommand InitialScanCommand { get; }
        public ICommand QuickScanCommand { get; }
        public ICommand DetectDuplicatesCommand { get; }
        public ICommand DeleteSelectedDuplicatesCommand { get; }
        public ICommand MoveSelectedDuplicatesCommand { get; }
        public ICommand ExportDuplicateListCommand { get; }
        public ICommand ClearDuplicateSelectionCommand { get; }
        public ICommand ToggleGroupExpandCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand DryRunCommand { get; }
        public ICommand LiveMoveCommand { get; }
        public ICommand LiveCopyCommand { get; }
        public ICommand ClearQueueCommand { get; }
        public ICommand AddExceptionCommand { get; }
        public ICommand RemoveExceptionCommand { get; }
        public ICommand RefreshStatisticsCommand { get; }
        public ICommand TestNotificationsCommand { get; }

        // Automation (Tier 1) commands
        public ICommand AddRuleCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand AddRuleConditionCommand { get; }
        public ICommand RemoveRuleConditionCommand { get; }
        public ICommand BrowseRuleDestinationCommand { get; }
        public ICommand AddWatchFolderCommand { get; }
        public ICommand RemoveWatchFolderCommand { get; }
        public ICommand StartWatchingCommand { get; }
        public ICommand StopWatchingCommand { get; }
        public ICommand StartScheduleCommand { get; }
        public ICommand StopScheduleCommand { get; }
        public ICommand RunSweepNowCommand { get; }
        public ICommand ClearAutomationLogCommand { get; }
        
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
            
            // Initialize commands
            BrowseSourceCommand = new RelayCommand(_ => BrowseSource());
            BrowseDestinationCommand = new RelayCommand(_ => BrowseDestination());
            AddSourceFolderCommand = new RelayCommand(_ => AddSourceFolder());
            RemoveSourceFolderCommand = new RelayCommand(RemoveSourceFolder);
            DetectEngineCommand = new RelayCommand(_ => DetectEngine());
            AnalyzeSpaceCommand = new RelayCommand(_ => AnalyzeSpace());
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            ClearConfigCommand = new RelayCommand(_ => ClearConfig());
            InitialScanCommand = new RelayCommand(_ => InitialScan());
            QuickScanCommand = new RelayCommand(_ => QuickScan());
            DetectDuplicatesCommand = new RelayCommand(_ => DetectDuplicates());
            DeleteSelectedDuplicatesCommand = new RelayCommand(_ => DeleteSelectedDuplicates(), _ => HasSelectedDuplicates());
            MoveSelectedDuplicatesCommand = new RelayCommand(_ => MoveSelectedDuplicates(), _ => HasSelectedDuplicates());
            ExportDuplicateListCommand = new RelayCommand(_ => ExportDuplicateList(), _ => DuplicateGroups.Count > 0);
            ClearDuplicateSelectionCommand = new RelayCommand(_ => ClearDuplicateSelection(), _ => HasSelectedDuplicates());
            ToggleGroupExpandCommand = new RelayCommand(param => ToggleGroupExpand(param as Services.DuplicateGroup));
            UndoCommand = new RelayCommand(_ => Undo(), _ => CanUndo());
            DryRunCommand = new RelayCommand(_ => DryRun());
            LiveMoveCommand = new RelayCommand(_ => LiveMove());
            LiveCopyCommand = new RelayCommand(_ => LiveCopy());
            ClearQueueCommand = new RelayCommand(_ => ClearQueue());
            AddExceptionCommand = new RelayCommand(_ => AddException());
            RemoveExceptionCommand = new RelayCommand(RemoveException);

            // Automation (Tier 1) command wiring
            AddRuleCommand = new RelayCommand(_ => AddRule());
            RemoveRuleCommand = new RelayCommand(RemoveRule);
            AddRuleConditionCommand = new RelayCommand(_ => AddRuleCondition());
            RemoveRuleConditionCommand = new RelayCommand(RemoveRuleCondition);
            BrowseRuleDestinationCommand = new RelayCommand(_ => BrowseRuleDestination());
            AddWatchFolderCommand = new RelayCommand(_ => AddWatchFolder());
            RemoveWatchFolderCommand = new RelayCommand(RemoveWatchFolder);
            StartWatchingCommand = new RelayCommand(_ => StartWatching());
            StopWatchingCommand = new RelayCommand(_ => StopWatching());
            StartScheduleCommand = new RelayCommand(_ => StartSchedule());
            StopScheduleCommand = new RelayCommand(_ => StopSchedule());
            RunSweepNowCommand = new RelayCommand(async _ => await RunSweepNow());
            ClearAutomationLogCommand = new RelayCommand(_ => AutomationLog.Clear());
            RefreshStatisticsCommand = new RelayCommand(_ => RefreshStatistics());
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
        public void SetBannerNotification(Controls.BannerNotification banner)
        {
            _bannerNotification = banner;
        }

        /// <summary>
        /// Show completion banner with statistics
        /// </summary>
        private void ShowCompletionBanner(string operation, string statistics, string icon = "✅")
        {
            if (_bannerNotification != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _bannerNotification.Show(
                        title: $"{operation.ToUpper()} COMPLETE!",
                        message: statistics,
                        icon: icon,
                        autoDismissSeconds: 15  // Auto-dismiss after 15 seconds
                    );
                });
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

                // Automation (Tier 1) persistence
                config.Rules.AddRange(Rules);
                config.WatchFolders.AddRange(WatchFolders);
                config.WatchIncludeSubfolders = WatchIncludeSubfolders;
                config.ScheduleEnabled = ScheduleEnabled;
                config.ScheduleIntervalMinutes = ScheduleIntervalMinutes;
                config.ScheduleRunOnStart = ScheduleRunOnStart;

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
            StatusMessage = "Running initial scan...";
            FileQueue.Clear();
            PendingCount = 0;
            MovedCount = 0;
            FailedCount = 0;

            // Send start notification
            _toastService.ShowOperationStarted("Initial Scan", $"Scanning {SourceFolder}");

            try
            {
                var progress = new Progress<double>(percent =>
                {
                    ProgressValue = percent;
                    StatusMessage = $"Scanning... {percent:F1}% complete";
                });

                var results = await _fileScanner.ScanDirectoryAsync(SourceFolder, SelectedScanMode, progress);

                // Apply exception filters
                results = ApplyExceptionFilters(results);

                foreach (var entry in results)
                {
                    FileQueue.Add(entry);
                }

                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                LastOperationDuration = FormatDuration(duration);

                PendingCount = results.Count;
                ProgressValue = 0;
                StatusMessage = $"Scan complete! Found {results.Count} files in {LastOperationDuration}";
                
                // Show completion banner
                ShowCompletionBanner("Initial Scan", 
                    $"Found {results.Count} files  |  Duration: {LastOperationDuration}  |  Ready to organize", 
                    "🔍");
                
                // Add to history
                AddHistoryEntry("Initial Scan", results.Count, results.Count, "Success");
                
                // Update statistics
                TotalOperations++;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                LastOperationDuration = FormatDuration(duration);

                ProgressValue = 0;
                StatusMessage = $"Scan failed: {ex.Message}";
                
                // Send failure notification
                _toastService.ShowOperationFailed("Initial Scan", ex.Message);
                
                System.Windows.MessageBox.Show($"Error during scan:\n{ex.Message}", 
                    "Scan Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                
                // Add failed history entry
                AddHistoryEntry("Initial Scan", 0, 0, "Failed");
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
            StatusMessage = "Running quick scan (top-level only)...";
            FileQueue.Clear();
            PendingCount = 0;
            MovedCount = 0;
            FailedCount = 0;

            // Send start notification
            _toastService.ShowOperationStarted("Quick Scan", $"Scanning {SourceFolder} (top-level only)");

            try
            {
                var results = _fileScanner.QuickScan(SourceFolder);

                // Apply exception filters
                results = ApplyExceptionFilters(results);

                foreach (var entry in results)
                {
                    FileQueue.Add(entry);
                }

                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                LastOperationDuration = FormatDuration(duration);

                PendingCount = results.Count;
                StatusMessage = $"Quick scan complete! Found {results.Count} files in {LastOperationDuration}";
                
                // Show completion banner
                ShowCompletionBanner("Quick Scan", 
                    $"Found {results.Count} files in top-level directory  |  Duration: {LastOperationDuration}", 
                    "⚡");
                
                // Add to history
                AddHistoryEntry("Quick Scan", results.Count, results.Count, "Success");
                
                // Update statistics
                TotalOperations++;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                LastOperationDuration = FormatDuration(duration);

                StatusMessage = $"Quick scan failed: {ex.Message}";
                
                // Send failure notification
                _toastService.ShowOperationFailed("Quick Scan", ex.Message);
                
                System.Windows.MessageBox.Show($"Error during quick scan:\n{ex.Message}", 
                    "Scan Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                
                // Add failed history entry
                AddHistoryEntry("Quick Scan", 0, 0, "Failed");
            }
        }

        private async void DetectDuplicates()
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
            StatusMessage = "Detecting duplicates...";

            // Send start notification
            _toastService.ShowOperationStarted("Duplicate Detection", $"Scanning {SourceFolder} for duplicates");

            try
            {
                var detector = new Services.DuplicateDetector();
                var progress = new Progress<double>(percent =>
                {
                    ProgressValue = percent;
                    StatusMessage = $"Scanning for duplicates... {percent:F1}% complete";
                });

                // Use quick scan or full scan based on setting
                Services.DuplicateDetectionResult result;
                if (UseQuickScan)
                {
                    StatusMessage = "Quick scanning for duplicates (size-based)...";
                    result = await detector.QuickDetectDuplicatesAsync(SourceFolder, SelectedScanMode, progress);
                }
                else
                {
                    StatusMessage = "Scanning for duplicates (SHA256)...";
                    result = await detector.DetectDuplicatesAsync(SourceFolder, SelectedScanMode, progress);
                }

                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                LastOperationDuration = FormatDuration(duration);

                DuplicateGroupsFound = result.DuplicateGroupCount;
                WastedSpaceGB = result.WastedSpaceGB;
                TotalDuplicateFiles = result.TotalDuplicateFiles;
                ProgressValue = 0;
                
                // Populate DuplicateGroups collection for UI
                DuplicateGroups.Clear();
                foreach (var group in result.DuplicateGroups)
                {
                    DuplicateGroups.Add(group);
                }
                
                // Reset selection statistics
                SelectedForDeletion = 0;
                SelectedDeletionSpaceGB = 0;

                StatusMessage = $"Duplicate detection complete! Found {result.DuplicateGroupCount} groups ({result.TotalDuplicateFiles} duplicate files, {result.WastedSpaceGB:F2} GB wasted) in {LastOperationDuration}";

                // Show completion banner
                string bannerMessage;
                string bannerIcon;
                if (result.DuplicateGroupCount > 0)
                {
                    bannerMessage = $"Scanned: {result.TotalFilesScanned:N0} files  |  " +
                                   $"Found: {result.DuplicateGroupCount:N0} groups ({result.TotalDuplicateFiles:N0} duplicates)  |  " +
                                   $"Wasted: {result.WastedSpaceGB:F2} GB  |  Duration: {LastOperationDuration}\n" +
                                   $"→ View the Duplicates tab to manage them";
                    bannerIcon = "🔍";
                }
                else
                {
                    bannerMessage = $"No duplicates found!  |  Scanned: {result.TotalFilesScanned:N0} files  |  Duration: {LastOperationDuration}";
                    bannerIcon = "✨";
                }
                
                ShowCompletionBanner("Duplicate Detection", bannerMessage, bannerIcon);

                // Add to history
                AddHistoryEntry("Detect Duplicates", result.TotalFilesScanned, result.DuplicateGroupCount, "Success");
                TotalOperations++;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                LastOperationDuration = FormatDuration(duration);

                ProgressValue = 0;
                StatusMessage = $"Duplicate detection failed: {ex.Message}";
                
                // Send failure notification
                _toastService.ShowOperationFailed("Duplicate Detection", ex.Message);
                
                System.Windows.MessageBox.Show($"Error detecting duplicates:\n{ex.Message}",
                    "Detection Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

                // Add failed history entry
                AddHistoryEntry("Detect Duplicates", 0, 0, "Failed");
            }
        }
        
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
                
                Services.DuplicateFile keepFile = null;
                
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
            StatusMessage = "Deleting duplicate files...";
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
                
                ProgressValue = ((deleted + failed) / (double)selectedFiles.Count) * 100;
                StatusMessage = $"Deleting duplicates... {deleted + failed}/{selectedFiles.Count}";
            }
            
            stopwatch.Stop();
            ProgressValue = 0;
            
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
            StatusMessage = $"Deletion complete! Deleted {deleted} files, freed {spaceGB:F2} GB";
            
            var message = $"Deleted: {deleted} files  |  Failed: {failed} files  |  Space Freed: {spaceGB:F2} GB  |  Duration: {FormatDuration(stopwatch.Elapsed)}";
            
            if (failed > 0 && failedFiles.Count <= 3)
            {
                message += "\n\nFailed: " + string.Join(", ", failedFiles.Select(f => System.IO.Path.GetFileName(f)));
            }
            else if (failed > 0)
            {
                message += $"\n\n{failed} files failed (check permissions)";
            }
            
            ShowCompletionBanner("Deletion", message, "🗑️");
            
            // Add to history
            AddHistoryEntry("Delete Duplicates", deleted + failed, deleted, 
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
            StatusMessage = "Moving duplicate files...";
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
                
                ProgressValue = ((moved + failed) / (double)selectedFiles.Count) * 100;
            }
            
            ProgressValue = 0;
            StatusMessage = $"Move complete! Moved {moved} files";
            
            System.Windows.MessageBox.Show(
                $"Move Complete!\n\n" +
                $"Moved: {moved} files\n" +
                $"Failed: {failed} files\n" +
                $"Destination: {destinationFolder}",
                "Move Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            
            // Refresh duplicate groups
            var detector = new Services.DuplicateDetector();
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
        
        private void ToggleGroupExpand(Services.DuplicateGroup group)
        {
            if (group != null)
            {
                group.IsExpanded = !group.IsExpanded;
            }
        }

        private bool CanUndo()
        {
            return _lastMoveOperation != null && _lastMoveOperation.Count > 0;
        }

        private async void Undo()
        {
            if (_lastMoveOperation == null || _lastMoveOperation.Count == 0)
            {
                ShowCompletionBanner("Nothing to Undo", 
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

            StatusMessage = "Undoing last operation...";
            int successCount = 0;
            int failedCount = 0;

            try
            {
                for (int i = 0; i < _lastMoveOperation.Count; i++)
                {
                    var entry = _lastMoveOperation[i];
                    
                    try
                    {
                        // Only undo if it was a move operation
                        if (entry.Status == "Moved" && !string.IsNullOrEmpty(entry.DestinationPath))
                        {
                            if (System.IO.File.Exists(entry.DestinationPath))
                            {
                                // Ensure source directory exists
                                var sourceDir = System.IO.Path.GetDirectoryName(entry.SourcePath);
                                if (!System.IO.Directory.Exists(sourceDir))
                                {
                                    System.IO.Directory.CreateDirectory(sourceDir);
                                }

                                // Move file back to original location
                                System.IO.File.Move(entry.DestinationPath, entry.SourcePath, true);
                                successCount++;
                            }
                            else
                            {
                                failedCount++; // File doesn't exist at destination
                            }
                        }

                        ProgressValue = ((double)(i + 1) / _lastMoveOperation.Count) * 100;
                        StatusMessage = $"Undoing... {ProgressValue:F1}%";
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to undo file {entry.FileName}: {ex.Message}");
                        failedCount++;
                    }
                }

                ProgressValue = 0;
                _lastMoveOperation.Clear(); // Clear after undo attempt

                StatusMessage = $"Undo complete! Restored: {successCount}, Failed: {failedCount}";

                // Add to history
                AddHistoryEntry("Undo Operation", successCount + failedCount, successCount, 
                    successCount > 0 ? "Success" : "Failed");
                TotalOperations++;

                ShowCompletionBanner("Undo Complete", 
                    $"Restored: {successCount} files  |  Failed: {failedCount} files  |  Files returned to original locations", 
                    successCount > 0 ? "↩️" : "⚠️");
            }
            catch (Exception ex)
            {
                ProgressValue = 0;
                StatusMessage = $"Undo failed: {ex.Message}";
                ShowCompletionBanner("Undo Error", 
                    $"Error during undo operation: {ex.Message}", 
                    "❌");
            }
        }

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
            
            StatusMessage = "Running dry run (preview)...";

            try
            {
                var config = new Config
                {
                    OperationMode = OperationMode,
                    StructureMode = StructureMode,
                    ConflictResolution = ConflictResolution,
                    SourceFolder = SourceFolder
                };

                var engine = new Services.MoveEngine(config);

                // Build destination paths for preview
                int wouldMove = 0;
                int wouldSkip = 0;
                long totalBytes = 0;

                foreach (var entry in FileQueue)
                {
                    // Simulate what would happen
                    var destPath = BuildPreviewDestPath(entry);
                    
                    if (System.IO.File.Exists(destPath))
                    {
                        if (ConflictResolution == FileConflictResolution.Skip)
                        {
                            wouldSkip++;
                            continue;
                        }
                    }

                    wouldMove++;
                    totalBytes += entry.SizeBytes;
                }

                // Add to history
                AddHistoryEntry("Dry Run", FileQueue.Count, wouldMove, "Success");
                TotalOperations++;
                
                double sizeGB = totalBytes / (1024.0 * 1024.0 * 1024.0);
                StatusMessage = $"Dry run complete! Would process {wouldMove} files ({sizeGB:F2} GB).";
                
                System.Windows.MessageBox.Show(
                    $"Dry Run Preview:\n\n" +
                    $"Total files in queue: {FileQueue.Count}\n" +
                    $"Would {OperationMode.ToString().ToLower()}: {wouldMove}\n" +
                    $"Would skip: {wouldSkip}\n" +
                    $"Total size: {sizeGB:F2} GB\n" +
                    $"Structure: {StructureMode}\n" +
                    $"Conflict handling: {ConflictResolution}\n\n" +
                    $"No files were actually moved or copied.",
                    "Dry Run Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Dry run failed: {ex.Message}";
                System.Windows.MessageBox.Show($"Error during dry run:\n{ex.Message}",
                    "Dry Run Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private string BuildPreviewDestPath(QueueEntry entry)
        {
            var fileName = System.IO.Path.GetFileName(entry.SourcePath);

            switch (StructureMode)
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
            StatusMessage = "Executing live move operation...";

            // Send start notification
            _toastService.ShowOperationStarted("Live Move", $"Moving {FileQueue.Count} files to {DestinationFolder}");

            try
            {
                var config = new Config
                {
                    OperationMode = FileOperationMode.Move,
                    StructureMode = StructureMode,
                    ConflictResolution = ConflictResolution,
                    SourceFolder = SourceFolder
                };

                var queueList = FileQueue.ToList();

                // Create and save initial resume state
                _currentResumeState = _resumeStateManager.CreateState(
                    FileOperationMode.Move,
                    SourceFolder,
                    DestinationFolder,
                    queueList);
                _resumeStateManager.SaveState(_currentResumeState);
                _filesProcessedSinceLastSave = 0;

                var engine = new Services.MoveEngine(config);
                var progress = new Progress<Services.OperationProgress>(p =>
                {
                    ProgressValue = p.PercentComplete;
                    StatusMessage = $"Moving... {p.PercentComplete:F1}% ({p.ProcessedFiles}/{p.TotalFiles}) - {p.CurrentFile}";
                    MovedCount = p.ProcessedFiles;
                    PendingCount = p.TotalFiles - p.ProcessedFiles;

                    // Update resume state periodically
                    _filesProcessedSinceLastSave++;
                    if (_filesProcessedSinceLastSave >= FilesPerStateSave)
                    {
                        UpdateResumeState(queueList.Take(p.ProcessedFiles).ToList());
                        _filesProcessedSinceLastSave = 0;
                    }
                });

                // Status callback for real-time verification feedback
                Action<string> statusCallback = (message) =>
                {
                    StatusMessage = message;
                };

                var opResult = await engine.ProcessQueueAsync(queueList, DestinationFolder, statusCallback, progress);

                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                LastOperationDuration = FormatDuration(duration);

                ProgressValue = 0;
                MovedCount = opResult.SuccessCount;
                FailedCount = opResult.FailedCount;
                PendingCount = 0;
                TotalFilesOrganized += opResult.SuccessCount;
                TotalOperations++;
                DataProcessedGB += opResult.TotalBytesProcessed / (1024.0 * 1024.0 * 1024.0);
                
                // Update verification statistics
                TotalFilesVerified += opResult.FilesVerified;
                VerificationPassed += opResult.VerificationPassed;
                VerificationFailed += opResult.VerificationFailed;
                VerificationRetried += opResult.VerificationRetried;
                OnPropertyChanged(nameof(VerificationSuccessRate));

                // Save for undo - only save successfully moved files
                _lastMoveOperation.Clear();
                _lastMoveOperation.AddRange(queueList.Where(e => e.Status == "Moved").ToList());

                // Clear resume state on successful completion
                _resumeStateManager.ClearState();
                _currentResumeState = null;

                // Add to history
                string status = opResult.SuccessCount == opResult.TotalFiles ? "Success" : 
                               opResult.SuccessCount > 0 ? $"Partial ({opResult.SuccessCount}/{opResult.TotalFiles})" : "Failed";
                AddHistoryEntry("Live Move", opResult.TotalFiles, opResult.SuccessCount, status,
                    opResult.FilesVerified, opResult.VerificationPassed, opResult.VerificationFailed, opResult.VerificationRetried);

                StatusMessage = $"Move complete! Success: {opResult.SuccessCount}, Failed: {opResult.FailedCount}, Skipped: {opResult.SkippedCount} in {LastOperationDuration}";
                
                // Show completion banner
                ShowCompletionBanner("Move Operation",
                    $"Moved: {opResult.SuccessCount}/{opResult.TotalFiles} files ({DataProcessedGB:F2} GB)  |  Failed: {opResult.FailedCount}  |  Skipped: {opResult.SkippedCount}  |  Duration: {LastOperationDuration}",
                    "📦");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                LastOperationDuration = FormatDuration(duration);

                StatusMessage = $"Move failed: {ex.Message}";
                
                // Send failure notification
                _toastService.ShowOperationFailed("Live Move", ex.Message);
                
                AddHistoryEntry("Live Move", 0, 0, "Failed");
                
                // Resume state is left on disk for potential recovery
                
                System.Windows.MessageBox.Show($"Error during move operation:\n{ex.Message}",
                    "Move Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

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
            StatusMessage = "Executing live copy operation...";

            // Send start notification
            _toastService.ShowOperationStarted("Live Copy", $"Copying {FileQueue.Count} files to {DestinationFolder}");

            try
            {
                var config = new Config
                {
                    OperationMode = FileOperationMode.Copy,
                    StructureMode = StructureMode,
                    ConflictResolution = ConflictResolution,
                    SourceFolder = SourceFolder
                };

                var queueList = FileQueue.ToList();

                // Create and save initial resume state
                _currentResumeState = _resumeStateManager.CreateState(
                    FileOperationMode.Copy,
                    SourceFolder,
                    DestinationFolder,
                    queueList);
                _resumeStateManager.SaveState(_currentResumeState);
                _filesProcessedSinceLastSave = 0;

                var engine = new Services.MoveEngine(config);
                var progress = new Progress<Services.OperationProgress>(p =>
                {
                    ProgressValue = p.PercentComplete;
                    StatusMessage = $"Copying... {p.PercentComplete:F1}% ({p.ProcessedFiles}/{p.TotalFiles}) - {p.CurrentFile}";
                    MovedCount = p.ProcessedFiles; // Reusing MovedCount for copied files
                    PendingCount = p.TotalFiles - p.ProcessedFiles;

                    // Update resume state periodically
                    _filesProcessedSinceLastSave++;
                    if (_filesProcessedSinceLastSave >= FilesPerStateSave)
                    {
                        UpdateResumeState(queueList.Take(p.ProcessedFiles).ToList());
                        _filesProcessedSinceLastSave = 0;
                    }
                });

                // Status callback for real-time verification feedback
                Action<string> statusCallback = (message) =>
                {
                    StatusMessage = message;
                };

                var opResult = await engine.ProcessQueueAsync(queueList, DestinationFolder, statusCallback, progress);

                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                LastOperationDuration = FormatDuration(duration);

                ProgressValue = 0;
                MovedCount = opResult.SuccessCount;
                FailedCount = opResult.FailedCount;
                PendingCount = 0;
                TotalFilesOrganized += opResult.SuccessCount;
                TotalOperations++;
                DataProcessedGB += opResult.TotalBytesProcessed / (1024.0 * 1024.0 * 1024.0);
                
                // Update verification statistics
                TotalFilesVerified += opResult.FilesVerified;
                VerificationPassed += opResult.VerificationPassed;
                VerificationFailed += opResult.VerificationFailed;
                VerificationRetried += opResult.VerificationRetried;
                OnPropertyChanged(nameof(VerificationSuccessRate));

                // Clear resume state on successful completion
                _resumeStateManager.ClearState();
                _currentResumeState = null;

                // Add to history
                string status = opResult.SuccessCount == opResult.TotalFiles ? "Success" : 
                               opResult.SuccessCount > 0 ? $"Partial ({opResult.SuccessCount}/{opResult.TotalFiles})" : "Failed";
                AddHistoryEntry("Live Copy", opResult.TotalFiles, opResult.SuccessCount, status,
                    opResult.FilesVerified, opResult.VerificationPassed, opResult.VerificationFailed, opResult.VerificationRetried);

                StatusMessage = $"Copy complete! Success: {opResult.SuccessCount}, Failed: {opResult.FailedCount}, Skipped: {opResult.SkippedCount} in {LastOperationDuration}";
                
                // Show completion banner
                ShowCompletionBanner("Copy Operation",
                    $"Copied: {opResult.SuccessCount}/{opResult.TotalFiles} files ({DataProcessedGB:F2} GB)  |  Failed: {opResult.FailedCount}  |  Skipped: {opResult.SkippedCount}  |  Duration: {LastOperationDuration}",
                    "📋");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                LastOperationDuration = FormatDuration(duration);

                StatusMessage = $"Copy failed: {ex.Message}";
                
                // Send failure notification
                _toastService.ShowOperationFailed("Live Copy", ex.Message);
                
                AddHistoryEntry("Live Copy", 0, 0, "Failed");
                
                // Resume state is left on disk for potential recovery
                
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
            StatusMessage = "Queue cleared";
        }

        /// <summary>
        /// Logs a verification result
        /// </summary>
        public void LogVerification(VerificationLog log)
        {
            _verificationLogs.Add(log);
            
            TotalFilesVerified++;
            
            if (log.Passed)
            {
                VerificationPassed++;
            }
            else
            {
                VerificationFailed++;
            }
            
            if (log.RetryCount > 0)
            {
                VerificationRetried++;
            }
            
            // Update success rate
            OnPropertyChanged(nameof(VerificationSuccessRate));
            OnPropertyChanged(nameof(RecentVerificationFailures));
        }

        /// <summary>
        /// Updates queue entry with verification result
        /// </summary>
        public void UpdateQueueEntryVerification(QueueEntry entry, CopyResult result)
        {
            if (result != null)
            {
                entry.Verified = result.Verified;
                entry.VerificationRetries = result.VerificationRetries;
                entry.VerificationFailed = result.VerificationFailed;
                
                if (result.VerificationMode == VerificationMode.None)
                    entry.VerificationMethod = "None";
                else if (!string.IsNullOrEmpty(result.SourceHash))
                    entry.VerificationMethod = "✅ SHA256";
                else if (result.Verified)
                    entry.VerificationMethod = "✅ Size";
                else
                    entry.VerificationMethod = "❌ Failed";
                
                if (result.VerificationRetries > 0 && result.Verified)
                    entry.VerificationMethod += $" (Retry {result.VerificationRetries})";
            }
        }

        /// <summary>
        /// Updates the resume state with completed files
        /// </summary>
        private void UpdateResumeState(List<QueueEntry> processedEntries)
        {
            if (_currentResumeState != null)
            {
                _resumeStateManager.UpdateState(_currentResumeState, processedEntries);
            }
        }

        /// <summary>
        /// Resumes an incomplete operation
        /// </summary>
        public async void ResumeOperation(ResumeState state)
        {
            if (state == null || state.RemainingQueue == null || state.RemainingQueue.Count == 0)
            {
                System.Windows.MessageBox.Show("Invalid resume state.", 
                    "Resume Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            StatusMessage = $"Resuming {state.OperationMode} operation...";

            try
            {
                var config = new Config
                {
                    OperationMode = state.OperationMode,
                    StructureMode = StructureMode,
                    ConflictResolution = ConflictResolution,
                    SourceFolder = state.SourceFolder
                };

                _currentResumeState = state;
                _filesProcessedSinceLastSave = 0;

                var engine = new Services.MoveEngine(config);
                var totalFiles = state.TotalFiles;
                var alreadyCompleted = state.CompletedCount;

                var progress = new Progress<Services.OperationProgress>(p =>
                {
                    var actualProcessed = alreadyCompleted + p.ProcessedFiles;
                    var actualPercent = (double)actualProcessed / totalFiles * 100;
                    
                    ProgressValue = actualPercent;
                    StatusMessage = $"Resuming {state.OperationMode}... {actualPercent:F1}% ({actualProcessed}/{totalFiles}) - {p.CurrentFile}";
                    MovedCount = actualProcessed;
                    PendingCount = totalFiles - actualProcessed;

                    // Update resume state periodically
                    _filesProcessedSinceLastSave++;
                    if (_filesProcessedSinceLastSave >= FilesPerStateSave)
                    {
                        UpdateResumeState(state.RemainingQueue.Take(p.ProcessedFiles).ToList());
                        _filesProcessedSinceLastSave = 0;
                    }
                });

                // Status callback for real-time verification feedback
                Action<string> statusCallback = (message) =>
                {
                    StatusMessage = message;
                };

                var opResult = await engine.ProcessQueueAsync(state.RemainingQueue, state.DestinationFolder, statusCallback, progress);

                ProgressValue = 0;
                var totalSuccess = alreadyCompleted + opResult.SuccessCount;
                MovedCount = totalSuccess;
                FailedCount = opResult.FailedCount;
                PendingCount = 0;
                TotalFilesOrganized += opResult.SuccessCount;
                TotalOperations++;
                DataProcessedGB += opResult.TotalBytesProcessed / (1024.0 * 1024.0 * 1024.0);

                // Clear resume state on successful completion
                _resumeStateManager.ClearState();
                _currentResumeState = null;

                // Add to history
                string status = totalSuccess == totalFiles ? "Success" : 
                               totalSuccess > 0 ? $"Partial ({totalSuccess}/{totalFiles})" : "Failed";
                AddHistoryEntry($"Resume {state.OperationMode}", totalFiles, totalSuccess, status);

                StatusMessage = $"Resume complete! Success: {totalSuccess}, Failed: {opResult.FailedCount}, Skipped: {opResult.SkippedCount}";
                
                System.Windows.MessageBox.Show(
                    $"Resume operation completed!\n\nTotal Success: {totalSuccess}/{totalFiles}\nFailed: {opResult.FailedCount}\nSkipped: {opResult.SkippedCount}",
                    "Resume Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Resume failed: {ex.Message}";
                AddHistoryEntry("Resume Operation", 0, 0, "Failed");
                
                // Resume state is left on disk for another attempt
                
                System.Windows.MessageBox.Show($"Error during resume operation:\n{ex.Message}",
                    "Resume Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Undoes a partially completed operation from resume state
        /// </summary>
        public async void UndoFromResume(ResumeState state)
        {
            if (state == null || state.CompletedFiles == null || state.CompletedFiles.Count == 0)
            {
                ShowCompletionBanner("Nothing to Undo", 
                    "No files found to undo from the incomplete operation.", 
                    "ℹ️");
                _resumeStateManager.ClearState();
                return;
            }

            StatusMessage = "Undoing incomplete operation...";
            int successCount = 0;
            int failedCount = 0;

            try
            {
                // Only undo move operations (not copy)
                if (state.OperationMode != FileOperationMode.Move)
                {
                    ShowCompletionBanner("Undo Not Available", 
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
                        // Find the entry in the original queue to get destination path
                        var entry = state.RemainingQueue.FirstOrDefault(e => e.SourcePath == sourcePath);
                        
                        if (entry != null && !string.IsNullOrEmpty(entry.DestinationPath))
                        {
                            if (System.IO.File.Exists(entry.DestinationPath))
                            {
                                // Ensure source directory exists
                                var sourceDir = System.IO.Path.GetDirectoryName(sourcePath);
                                if (!System.IO.Directory.Exists(sourceDir))
                                {
                                    System.IO.Directory.CreateDirectory(sourceDir);
                                }

                                // Move file back to original location
                                System.IO.File.Move(entry.DestinationPath, sourcePath, true);
                                successCount++;
                            }
                        }

                        ProgressValue = ((double)(i + 1) / state.CompletedFiles.Count) * 100;
                        StatusMessage = $"Undoing... {ProgressValue:F1}%";
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to undo file {sourcePath}: {ex.Message}");
                        failedCount++;
                    }
                }

                ProgressValue = 0;
                
                // Clear resume state after undo attempt
                _resumeStateManager.ClearState();
                _currentResumeState = null;

                StatusMessage = $"Undo from resume complete! Restored: {successCount}, Failed: {failedCount}";

                // Add to history
                AddHistoryEntry("Undo from Resume", state.CompletedFiles.Count, successCount, 
                    successCount > 0 ? "Success" : "Failed");
                TotalOperations++;

                ShowCompletionBanner("Undo Complete", 
                    $"Restored: {successCount} files  |  Failed: {failedCount} files  |  Incomplete operation reversed", 
                    successCount > 0 ? "↩️" : "⚠️");
            }
            catch (Exception ex)
            {
                ProgressValue = 0;
                StatusMessage = $"Undo from resume failed: {ex.Message}";
                ShowCompletionBanner("Undo Error", 
                    $"Error during undo operation: {ex.Message}", 
                    "❌");
            }
        }

        /// <summary>
        /// Checks for incomplete operations on startup (called by App.xaml.cs)
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
                        var dialog = new ResumeDialog(summary);
                        
                        var result = dialog.ShowDialog();
                        
                        if (result == true)
                        {
                            switch (dialog.SelectedAction)
                            {
                                case ResumeDialog.ResumeAction.Resume:
                                    // Restore UI state
                                    SourceFolder = state.SourceFolder;
                                    DestinationFolder = state.DestinationFolder;
                                    ResumeOperation(state);
                                    break;
                                    
                                case ResumeDialog.ResumeAction.Undo:
                                    UndoFromResume(state);
                                    break;
                                    
                                case ResumeDialog.ResumeAction.Cancel:
                                    _resumeStateManager.ClearState();
                                    StatusMessage = "Incomplete operation discarded";
                                    break;
                            }
                        }
                        else
                        {
                            // User closed dialog or clicked cancel
                            _resumeStateManager.ClearState();
                            StatusMessage = "Incomplete operation discarded";
                        }
                    }
                    else
                    {
                        // Invalid state, clean it up
                        _resumeStateManager.ClearState();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for incomplete operation: {ex.Message}");
                // Clean up any corrupted state
                try
                {
                    _resumeStateManager.ClearState();
                }
                catch { }
            }
        }

        /// <summary>
        /// Applies exception filters to the scan results
        /// </summary>
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

        private void AddException()
        {
            // First, ask if this is a folder or file
            var typeResult = System.Windows.MessageBox.Show(
                "Do you want to select a folder?\n\nClick 'Yes' for folder, 'No' for file, 'Cancel' to abort.",
                "Select Exception Type",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question);

            if (typeResult == System.Windows.MessageBoxResult.Cancel)
            {
                return;
            }

            bool isFolder = typeResult == System.Windows.MessageBoxResult.Yes;
            string selectedPath = null;

            if (isFolder)
            {
                // Show folder browser
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = "Select folder to add as exception from source";
                
                if (!string.IsNullOrEmpty(SourceFolder) && System.IO.Directory.Exists(SourceFolder))
                {
                    folderDialog.SelectedPath = SourceFolder;
                }

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    selectedPath = folderDialog.SelectedPath;
                }
            }
            else
            {
                // Show file browser
                var fileDialog = new System.Windows.Forms.OpenFileDialog();
                fileDialog.Title = "Select file to add as exception";
                fileDialog.Filter = "All Files (*.*)|*.*";
                
                if (!string.IsNullOrEmpty(SourceFolder) && System.IO.Directory.Exists(SourceFolder))
                {
                    fileDialog.InitialDirectory = SourceFolder;
                }

                if (fileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    selectedPath = fileDialog.FileName;
                }
            }

            if (!string.IsNullOrEmpty(selectedPath))
            {
                // Now ask for exception type (Exclude or Semi)
                var excludeResult = System.Windows.MessageBox.Show(
                    "Select Exception Type:\n\n" +
                    "YES = Exclude\n" +
                    "(Not copied at all, therefore not removed)\n\n" +
                    "NO = Semi-Exclude\n" +
                    "(Copied but only folder structure remains if folder, or just the file itself)",
                    "Select Exception Type",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                var exceptionType = excludeResult == System.Windows.MessageBoxResult.Yes 
                    ? ExceptionType.Exclude 
                    : ExceptionType.Semi;

                Exceptions.Add(new ExceptionFilter
                {
                    Path = selectedPath,
                    IsFolder = isFolder,
                    Type = exceptionType,
                    IsEnabled = true
                });

                string typeName = isFolder ? "folder" : "file";
                string excTypeName = exceptionType == ExceptionType.Exclude ? "Exclude" : "Semi-Exclude";
                StatusMessage = $"Exception added: {typeName} ({excTypeName})";
                
                System.Windows.MessageBox.Show(
                    $"Exception added: {System.IO.Path.GetFileName(selectedPath)} ({excTypeName})",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private void RemoveException(object parameter)
        {
            if (parameter is ExceptionFilter filter)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to remove this exception?\n\n" +
                    $"Path: {filter.Path}\n" +
                    $"Type: {filter.TypeDisplay}",
                    "Confirm Remove Exception",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    Exceptions.Remove(filter);
                    StatusMessage = "Exception removed";
                }
            }
        }

        // ======================= Automation (Tier 1) =======================

        private void AddRule()
        {
            var rule = new OrganizationRule { Name = "New Rule" };
            rule.Conditions.Add(new RuleCondition());
            Rules.Add(rule);
            SelectedRule = rule;
            StatusMessage = "Rule added — set its conditions and destination.";
        }

        private void RemoveRule(object parameter)
        {
            if (parameter is OrganizationRule rule)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Remove rule \"{rule.Name}\"?",
                    "Confirm Remove Rule",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    Rules.Remove(rule);
                    if (SelectedRule == rule) SelectedRule = null;
                    StatusMessage = "Rule removed.";
                }
            }
        }

        private void AddRuleCondition()
        {
            if (SelectedRule == null)
            {
                StatusMessage = "Select a rule first, then add a condition.";
                return;
            }
            SelectedRule.Conditions.Add(new RuleCondition());
            OnPropertyChanged(nameof(SelectedRule));
            StatusMessage = "Condition added to rule.";
        }

        private void RemoveRuleCondition(object parameter)
        {
            if (SelectedRule != null && parameter is RuleCondition condition)
            {
                SelectedRule.Conditions.Remove(condition);
                OnPropertyChanged(nameof(SelectedRule));
                StatusMessage = "Condition removed.";
            }
        }

        private void BrowseRuleDestination()
        {
            if (SelectedRule == null)
            {
                StatusMessage = "Select a rule first.";
                return;
            }
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SelectedRule.DestinationFolder = dialog.SelectedPath;
                OnPropertyChanged(nameof(SelectedRule));
                StatusMessage = $"Rule destination set: {dialog.SelectedPath}";
            }
        }

        private void AddWatchFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (!WatchFolders.Contains(dialog.SelectedPath))
                {
                    WatchFolders.Add(dialog.SelectedPath);
                    StatusMessage = $"Watch folder added: {dialog.SelectedPath}";
                }
            }
        }

        private void RemoveWatchFolder(object parameter)
        {
            if (parameter is string folder)
            {
                WatchFolders.Remove(folder);
                StatusMessage = "Watch folder removed.";
            }
        }

        private RuleEngine BuildRuleEngine() => new RuleEngine(Rules.ToList());

        private MoveEngine BuildAutomationMoveEngine()
        {
            var config = new Config
            {
                CopyEngine = CopyEngine.CustomFast, // automation uses the safe default engine
                PreserveTimestamps = PreserveTimestamps,
                VerificationMode = VerificationMode,
                VerifyExternalCopies = VerifyExternalCopies,
                RetryAttempts = RetryAttempts,
                RetryDelaySeconds = RetryDelaySeconds
            };
            return new MoveEngine(config);
        }

        private void AppendAutomationLog(string line)
        {
            var stamped = $"{DateTime.Now:HH:mm:ss}  {line}";
            // Marshal to the UI thread; watcher callbacks arrive off-thread.
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                AutomationLog.Insert(0, stamped);
                while (AutomationLog.Count > 200) AutomationLog.RemoveAt(AutomationLog.Count - 1);
            });
        }

        private List<string> EffectiveWatchFolders()
        {
            var folders = WatchFolders.ToList();
            if (folders.Count == 0)
                folders = SourceFolders.ToList(); // fall back to configured sources
            return folders;
        }

        private void StartWatching()
        {
            if (Rules.Count == 0)
            {
                StatusMessage = "Add at least one rule before starting the watcher.";
                return;
            }
            var folders = EffectiveWatchFolders();
            if (folders.Count == 0)
            {
                StatusMessage = "Add a watch folder (or a source folder) before starting.";
                return;
            }

            _folderWatcher?.Dispose();
            _folderWatcher = new FolderWatcherService(BuildRuleEngine(), BuildAutomationMoveEngine());
            _folderWatcher.Log += AppendAutomationLog;
            _folderWatcher.Start(folders, WatchIncludeSubfolders);

            IsWatching = _folderWatcher.IsRunning;
            StatusMessage = IsWatching ? "Folder watching started." : "Could not start watcher.";
        }

        private void StopWatching()
        {
            _folderWatcher?.Stop();
            IsWatching = false;
            StatusMessage = "Folder watching stopped.";
        }

        private void StartSchedule()
        {
            if (Rules.Count == 0)
            {
                StatusMessage = "Add at least one rule before starting the scheduler.";
                return;
            }
            var folders = EffectiveWatchFolders();
            if (folders.Count == 0)
            {
                StatusMessage = "Add a folder to sweep before starting the scheduler.";
                return;
            }

            _scheduler?.Dispose();
            _scheduler = new ScheduledSortService(BuildRuleEngine(), BuildAutomationMoveEngine());
            _scheduler.Log += AppendAutomationLog;
            _scheduler.Start(folders, WatchIncludeSubfolders, ScheduleIntervalMinutes, ScheduleRunOnStart);

            IsScheduleRunning = _scheduler.IsRunning;
            StatusMessage = "Scheduler started.";
        }

        private void StopSchedule()
        {
            _scheduler?.Stop();
            IsScheduleRunning = false;
            StatusMessage = "Scheduler stopped.";
        }

        private async System.Threading.Tasks.Task RunSweepNow()
        {
            if (Rules.Count == 0)
            {
                StatusMessage = "Add at least one rule first.";
                return;
            }
            var folders = EffectiveWatchFolders();
            if (folders.Count == 0)
            {
                StatusMessage = "Add a folder to sweep first.";
                return;
            }

            var sweeper = new ScheduledSortService(BuildRuleEngine(), BuildAutomationMoveEngine());
            sweeper.Log += AppendAutomationLog;
            StatusMessage = "Running one-time sweep...";
            await sweeper.RunNowAsync();
            sweeper.Dispose();
            StatusMessage = "Sweep complete.";
        }

        private void RefreshStatistics()
        {
            StatusMessage = "Statistics refreshed";
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
        /// Adds a history entry for an operation
        /// </summary>
        private void AddHistoryEntry(string mode, int filesScanned, int successCount, string status, 
            int filesVerified = 0, int verificationPassed = 0, int verificationFailed = 0, int verificationRetried = 0)
        {
            var entry = new HistoryEntry
            {
                Timestamp = DateTime.Now,
                Mode = mode,
                FilesScanned = filesScanned,
                SuccessCount = successCount,
                Status = status,
                FilesVerified = filesVerified,
                VerificationPassed = verificationPassed,
                VerificationFailed = verificationFailed,
                VerificationRetried = verificationRetried
            };

            History.Insert(0, entry);
            
            // Keep only last 50 entries in UI
            while (History.Count > 50)
            {
                History.RemoveAt(History.Count - 1);
            }

            // Persist to disk
            try
            {
                _historyManager.AddHistoryEntry(entry, History.ToList());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error persisting history: {ex.Message}");
            }
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

                    // Load automation (Tier 1) settings
                    if (config.Rules != null)
                        foreach (var rule in config.Rules) Rules.Add(rule);
                    if (config.WatchFolders != null)
                        foreach (var wf in config.WatchFolders) WatchFolders.Add(wf);
                    WatchIncludeSubfolders = config.WatchIncludeSubfolders;
                    ScheduleEnabled = config.ScheduleEnabled;
                    ScheduleIntervalMinutes = config.ScheduleIntervalMinutes > 0 ? config.ScheduleIntervalMinutes : 60;
                    ScheduleRunOnStart = config.ScheduleRunOnStart;

                    StatusMessage = "Configuration loaded";
                }

                // Load history
                var history = _historyManager.LoadHistory();
                foreach (var entry in history)
                {
                    History.Add(entry);
                }
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

        /// <summary>
        /// Format duration into readable string
        /// </summary>
        private string FormatDuration(System.TimeSpan duration)
        {
            if (duration.TotalSeconds < 1)
            {
                return $"{duration.TotalMilliseconds:F0}ms";
            }
            else if (duration.TotalMinutes < 1)
            {
                return $"{duration.TotalSeconds:F1}s";
            }
            else if (duration.TotalHours < 1)
            {
                return $"{duration.Minutes}m {duration.Seconds}s";
            }
            else
            {
                return $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s";
            }
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
