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
    /// Owns the Automation tab: organization rules, real-time folder watching, and
    /// scheduled sweeps.
    ///
    /// Depends only on two narrow interfaces rather than on MainViewModel:
    ///   IStatusSink               — to report status to the shared status bar
    ///   ITransferSettingsProvider — to honour the user's transfer settings and to fall
    ///                               back to configured source folders
    /// </summary>
    public class AutomationViewModel : ViewModelBase, IDisposable
    {
        private readonly IStatusSink _status;
        private readonly ITransferSettingsProvider _settings;

        private FolderWatcherService _folderWatcher;
        private ScheduledSortService _scheduler;

        public AutomationViewModel(IStatusSink status, ITransferSettingsProvider settings)
        {
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

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
        }

        // ---- Collections ----
        public ObservableCollection<OrganizationRule> Rules { get; } = new ObservableCollection<OrganizationRule>();
        public ObservableCollection<string> WatchFolders { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AutomationLog { get; } = new ObservableCollection<string>();

        // ---- Item sources for the tab's combo boxes ----
        public List<RuleConditionType> RuleConditionTypes { get; } =
            Enum.GetValues(typeof(RuleConditionType)).Cast<RuleConditionType>().ToList();
        public List<RuleMatchMode> RuleMatchModes { get; } =
            Enum.GetValues(typeof(RuleMatchMode)).Cast<RuleMatchMode>().ToList();

        // ---- State ----
        private OrganizationRule _selectedRule;
        public OrganizationRule SelectedRule
        {
            get => _selectedRule;
            set => SetProperty(ref _selectedRule, value);
        }

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

        // ---- Commands ----
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

        // ---- Rules ----
        private void AddRule()
        {
            var rule = new OrganizationRule { Name = "New Rule" };
            rule.Conditions.Add(new RuleCondition());
            Rules.Add(rule);
            SelectedRule = rule;
            _status.SetStatus("Rule added — set its conditions and destination.");
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
                    _status.SetStatus("Rule removed.");
                }
            }
        }

        private void AddRuleCondition()
        {
            if (SelectedRule == null)
            {
                _status.SetStatus("Select a rule first, then add a condition.");
                return;
            }
            SelectedRule.Conditions.Add(new RuleCondition());
            OnPropertyChanged(nameof(SelectedRule));
            _status.SetStatus("Condition added to rule.");
        }

        private void RemoveRuleCondition(object parameter)
        {
            if (SelectedRule != null && parameter is RuleCondition condition)
            {
                SelectedRule.Conditions.Remove(condition);
                OnPropertyChanged(nameof(SelectedRule));
                _status.SetStatus("Condition removed.");
            }
        }

        private void BrowseRuleDestination()
        {
            if (SelectedRule == null)
            {
                _status.SetStatus("Select a rule first.");
                return;
            }
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SelectedRule.DestinationFolder = dialog.SelectedPath;
                OnPropertyChanged(nameof(SelectedRule));
                _status.SetStatus($"Rule destination set: {dialog.SelectedPath}");
            }
        }

        // ---- Watch folders ----
        private void AddWatchFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (!WatchFolders.Contains(dialog.SelectedPath))
                {
                    WatchFolders.Add(dialog.SelectedPath);
                    _status.SetStatus($"Watch folder added: {dialog.SelectedPath}");
                }
            }
        }

        private void RemoveWatchFolder(object parameter)
        {
            if (parameter is string folder)
            {
                WatchFolders.Remove(folder);
                _status.SetStatus("Watch folder removed.");
            }
        }

        // ---- Engine helpers ----
        private RuleEngine BuildRuleEngine() => new RuleEngine(Rules.ToList());

        private MoveEngine BuildAutomationMoveEngine() => new MoveEngine(_settings.BuildAutomationConfig());

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
                folders = _settings.SourceFolders.ToList(); // fall back to configured sources
            return folders;
        }

        // ---- Watching ----
        private void StartWatching()
        {
            if (Rules.Count == 0)
            {
                _status.SetStatus("Add at least one rule before starting the watcher.");
                return;
            }
            var folders = EffectiveWatchFolders();
            if (folders.Count == 0)
            {
                _status.SetStatus("Add a watch folder (or a source folder) before starting.");
                return;
            }

            _folderWatcher?.Dispose();
            _folderWatcher = new FolderWatcherService(BuildRuleEngine(), BuildAutomationMoveEngine());
            _folderWatcher.Log += AppendAutomationLog;
            _folderWatcher.Start(folders, WatchIncludeSubfolders);

            IsWatching = _folderWatcher.IsRunning;
            _status.SetStatus(IsWatching ? "Folder watching started." : "Could not start watcher.");
        }

        private void StopWatching()
        {
            _folderWatcher?.Stop();
            IsWatching = false;
            _status.SetStatus("Folder watching stopped.");
        }

        // ---- Scheduling ----
        private void StartSchedule()
        {
            if (Rules.Count == 0)
            {
                _status.SetStatus("Add at least one rule before starting the scheduler.");
                return;
            }
            var folders = EffectiveWatchFolders();
            if (folders.Count == 0)
            {
                _status.SetStatus("Add a folder to sweep before starting the scheduler.");
                return;
            }

            _scheduler?.Dispose();
            _scheduler = new ScheduledSortService(BuildRuleEngine(), BuildAutomationMoveEngine());
            _scheduler.Log += AppendAutomationLog;
            _scheduler.Start(folders, WatchIncludeSubfolders, ScheduleIntervalMinutes, ScheduleRunOnStart);

            IsScheduleRunning = _scheduler.IsRunning;
            _status.SetStatus("Scheduler started.");
        }

        private void StopSchedule()
        {
            _scheduler?.Stop();
            IsScheduleRunning = false;
            _status.SetStatus("Scheduler stopped.");
        }

        private async Task RunSweepNow()
        {
            if (Rules.Count == 0)
            {
                _status.SetStatus("Add at least one rule first.");
                return;
            }
            var folders = EffectiveWatchFolders();
            if (folders.Count == 0)
            {
                _status.SetStatus("Add a folder to sweep first.");
                return;
            }

            var sweeper = new ScheduledSortService(BuildRuleEngine(), BuildAutomationMoveEngine());
            sweeper.Log += AppendAutomationLog;
            _status.SetStatus("Running one-time sweep...");
            await sweeper.RunNowAsync();
            sweeper.Dispose();
            _status.SetStatus("Sweep complete.");
        }

        public void Dispose()
        {
            _folderWatcher?.Dispose();
            _scheduler?.Dispose();
        }
    }
}
