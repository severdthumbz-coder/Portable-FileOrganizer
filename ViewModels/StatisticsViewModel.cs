using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using FileOrganizer.Commands;
using FileOrganizer.Models;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Owns the Statistics tab. Statistics is a read-model: it displays totals that the
    /// operational code (scans, live move/copy, duplicate detection, verification) increments.
    ///
    /// Those operational writes go through IStatsSink (which MainViewModel forwards to this
    /// object), so the Operations pipeline never touches these properties directly. The
    /// Statistics tab binds this ViewModel.
    /// </summary>
    public class StatisticsViewModel : ViewModelBase, IStatsSink
    {
        private readonly INotificationService _notify;

        public StatisticsViewModel(INotificationService notify)
        {
            _notify = notify ?? throw new ArgumentNullException(nameof(notify));
            RefreshStatisticsCommand = new RelayCommand(_ => RefreshStatistics());
        }

        // ---- Operation counters ----
        private int _totalFilesOrganized;
        public int TotalFilesOrganized
        {
            get => _totalFilesOrganized;
            set => SetProperty(ref _totalFilesOrganized, value);
        }

        private int _totalOperations;
        public int TotalOperations
        {
            get => _totalOperations;
            set => SetProperty(ref _totalOperations, value);
        }

        private double _dataProcessedGB;
        public double DataProcessedGB
        {
            get => _dataProcessedGB;
            set => SetProperty(ref _dataProcessedGB, value);
        }

        // ---- Duplicate stats (also written by DuplicatesViewModel via IStatsSink) ----
        private int _duplicateGroupsFound;
        public int DuplicateGroupsFound
        {
            get => _duplicateGroupsFound;
            set => SetProperty(ref _duplicateGroupsFound, value);
        }

        private double _wastedSpaceGB;
        public double WastedSpaceGB
        {
            get => _wastedSpaceGB;
            set => SetProperty(ref _wastedSpaceGB, value);
        }

        // ---- Verification stats ----
        private int _totalFilesVerified;
        public int TotalFilesVerified
        {
            get => _totalFilesVerified;
            set => SetProperty(ref _totalFilesVerified, value);
        }

        private int _verificationPassed;
        public int VerificationPassed
        {
            get => _verificationPassed;
            set => SetProperty(ref _verificationPassed, value);
        }

        private int _verificationFailed;
        public int VerificationFailed
        {
            get => _verificationFailed;
            set => SetProperty(ref _verificationFailed, value);
        }

        private int _verificationRetried;
        public int VerificationRetried
        {
            get => _verificationRetried;
            set => SetProperty(ref _verificationRetried, value);
        }

        public double VerificationSuccessRate =>
            TotalFilesVerified > 0 ? (double)VerificationPassed / TotalFilesVerified * 100 : 0;

        private readonly List<VerificationLog> _verificationLogs = new List<VerificationLog>();
        public List<VerificationLog> VerificationLogs => _verificationLogs;

        public List<VerificationLog> RecentVerificationFailures =>
            _verificationLogs.Where(x => !x.Passed).OrderByDescending(x => x.Timestamp).Take(10).ToList();

        public ICommand RefreshStatisticsCommand { get; }

        // ---- IStatsSink: the write channel used by operational code ----
        public void IncrementOperations() => TotalOperations++;

        /// <summary>Records the outcome of a completed batch operation.</summary>
        public void RecordOperation(int filesOrganized, double dataProcessedGB,
            int filesVerified, int verificationPassed, int verificationFailed, int verificationRetried)
        {
            TotalFilesOrganized += filesOrganized;
            TotalOperations++;
            DataProcessedGB += dataProcessedGB;

            TotalFilesVerified += filesVerified;
            VerificationPassed += verificationPassed;
            VerificationFailed += verificationFailed;
            VerificationRetried += verificationRetried;

            OnPropertyChanged(nameof(VerificationSuccessRate));
        }

        /// <summary>
        /// Logs a single verification result and updates the derived stats.
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

            OnPropertyChanged(nameof(VerificationSuccessRate));
            OnPropertyChanged(nameof(RecentVerificationFailures));
        }

        private void RefreshStatistics()
        {
            // Re-raise computed properties so the UI recalculates.
            OnPropertyChanged(nameof(VerificationSuccessRate));
            OnPropertyChanged(nameof(RecentVerificationFailures));
            _notify.SetStatus("Statistics refreshed");
        }
    }
}
