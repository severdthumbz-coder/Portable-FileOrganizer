using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FileOrganizer.Commands;
using FileOrganizer.Models;
using FileOrganizer.Services;

namespace FileOrganizer.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, INotificationService, ITransferSettingsProvider, IOperationsSettingsProvider, IConfigPersistence
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

        // Configuration-tab settings, enum lists, engine detection, and space analysis were
        // extracted to ConfigurationViewModel in Build 1.4.9 (the final strangle step). This
        // class keeps only the coordinator role plus the cross-tab Save/Clear/Load orchestration.

        private string _statusMessage = "Ready";

        #endregion

        #region Properties
        
        // ---- Configuration tab (extracted to ConfigurationViewModel in Build 1.4.9) ----
        // The last extraction. ConfigVM owns all Configuration-tab settings, enum lists,
        // engine detection and space analysis. MainViewModel keeps only the cross-tab
        // Save/Clear/Load orchestration (below) and re-points its settings-provider
        // interface impls to ConfigVM.*.
        public ConfigurationViewModel ConfigVM { get; }

        // Status
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string VersionInfo => "v5.0 build 1.4.9";



        // ---- Duplicates tab (extracted to DuplicatesViewModel in Build 1.4.6) ----
        public DuplicatesViewModel DuplicatesVM { get; }

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
            PreserveTimestamps = ConfigVM.PreserveTimestamps,
            VerificationMode = ConfigVM.VerificationMode,
            VerifyExternalCopies = ConfigVM.VerifyExternalCopies,
            RetryAttempts = ConfigVM.RetryAttempts,
            RetryDelaySeconds = ConfigVM.RetryDelaySeconds
        };

        // ---- Statistics tab (extracted to StatisticsViewModel in Build 1.4.7) ----
        // StatisticsViewModel is the IStatsSink; operational code writes via the _stats field.
        public StatisticsViewModel StatisticsVM { get; }

        // ---- Operations tab (extracted to OperationsViewModel in Build 1.4.8) ----
        public OperationsViewModel OperationsVM { get; }

        // ---- IOperationsSettingsProvider ----
        // Supplies the Configuration-tab settings the Operations pipeline reads, plus the
        // exception filter (which stays with the scan pipeline here).
        ScanMode IOperationsSettingsProvider.SelectedScanMode => ConfigVM.SelectedScanMode;
        DestinationStructureMode IOperationsSettingsProvider.StructureMode => ConfigVM.StructureMode;
        FileConflictResolution IOperationsSettingsProvider.ConflictResolution => ConfigVM.ConflictResolution;
        List<QueueEntry> IOperationsSettingsProvider.ApplyExceptionFilters(List<QueueEntry> entries) => ApplyExceptionFilters(entries);

        /// <summary>
        /// Startup hook (called from App). Delegates to OperationsViewModel, which owns the
        /// resume/undo pipeline.
        /// </summary>
        public void CheckForIncompleteOperation() => OperationsVM.CheckForIncompleteOperation();

        // Queue counters




        #endregion

        #region Commands
        
        // The Configuration-tab commands moved to ConfigurationViewModel in Build 1.4.9.
        // TestNotifications stays here: its button is on the Help tab (DataContext = MainViewModel).
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

            // Configuration tab (Build 1.4.9). Constructed early: MainViewModel's
            // IOperationsSettingsProvider / ITransferSettingsProvider impls and the Duplicates
            // scan-mode closure read ConfigVM.*, so it must exist before those are used. It owns
            // the storage-detection description text, so the SourceFolderChanged re-raise now
            // lives inside ConfigVM rather than here.
            ConfigVM = new ConfigurationViewModel(_session, _notifications, this);

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
                () => ConfigVM.SelectedScanMode);

            // Initialize commands. The Configuration-tab commands (Browse/Add/Remove/Detect/
            // Analyze/Save/Clear) moved to ConfigurationViewModel in Build 1.4.9; TestNotifications
            // stays here because its button lives on the Help tab (DataContext = MainViewModel).
            TestNotificationsCommand = new RelayCommand(_ => TestNotifications());

            // Load persisted configuration and history
            LoadPersistedData();
        }

        #endregion

        #region Command Methods

        // BrowseSource / BrowseDestination / AddSourceFolder / RemoveSourceFolder / DetectEngine
        // moved to ConfigurationViewModel in Build 1.4.9.

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

        // AnalyzeSpace moved to ConfigurationViewModel in Build 1.4.9.

        /// <summary>
        /// Cross-tab persistence orchestration (IConfigPersistence). Triggered by the Save button
        /// on the Configuration tab (ConfigVM forwards here). The Config-settings block is built by
        /// ConfigVM.BuildConfig(); this method adds the cross-tab pieces (SourceFolders, Exceptions,
        /// Automation) and writes the file, so persistence stays coordinated in one place.
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                var config = new Config();
                ConfigVM.BuildConfig(config);   // Configuration-tab settings block

                config.SourceFolders.AddRange(_session.SourceFolders);
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

        /// <summary>
        /// Cross-tab persistence orchestration (IConfigPersistence). Triggered by the Clear button
        /// on the Configuration tab (ConfigVM forwards here).
        /// </summary>
        public void ClearConfig()
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
                    // Configuration-tab settings block (incl. storage override) → ConfigVM.
                    ConfigVM.ApplyConfig(config);

                    // Cross-tab pieces stay here (session folders, exceptions, automation).
                    // Load source folders
                    foreach (var folder in config.SourceFolders)
                    {
                        _session.SourceFolders.Add(folder);
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
