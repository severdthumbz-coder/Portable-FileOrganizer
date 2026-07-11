using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using FileOrganizer.Commands;
using FileOrganizer.Models;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Owns the History tab: the list of past operations, its persistence, and the
    /// "Re-run" action.
    ///
    /// History entries are *created* from all over the operational code (scans, dedup,
    /// undo, live move/copy, resume — 16 call sites). Those callers go through
    /// MainViewModel.AddHistoryEntry, which forwards to AddEntry here, so the operational
    /// code is untouched.
    ///
    /// Depends only on:
    ///   INotificationService — status messages (for Re-run feedback)
    ///   SessionContext       — Re-run repopulates the session's folders / mode, and
    ///                          AddEntry reads them to remember what an operation acted on
    /// </summary>
    public class HistoryViewModel : ViewModelBase
    {
        private readonly INotificationService _notify;
        private readonly SessionContext _session;
        private readonly Services.HistoryManager _historyManager;

        public HistoryViewModel(INotificationService notify, SessionContext session, Services.HistoryManager historyManager)
        {
            _notify = notify ?? throw new ArgumentNullException(nameof(notify));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _historyManager = historyManager ?? throw new ArgumentNullException(nameof(historyManager));

            ReRunOperationCommand = new RelayCommand(ReRunOperation);
        }

        /// <summary>The most-recent-first list of past operations (capped at 50 in the UI).</summary>
        public ObservableCollection<HistoryEntry> History { get; } = new ObservableCollection<HistoryEntry>();

        public ICommand ReRunOperationCommand { get; }

        /// <summary>
        /// Records a completed operation. Called (via MainViewModel.AddHistoryEntry) from the
        /// operational code. Captures the session folders for Move/Copy so the entry can be
        /// re-run later.
        /// </summary>
        public void AddEntry(string mode, int filesScanned, int successCount, string status,
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
                VerificationRetried = verificationRetried,
                // Capture the folders so a Move/Copy operation can be re-run later.
                SourceFolder = (mode == "Move" || mode == "Copy") ? _session.SourceFolder : "",
                DestinationFolder = (mode == "Move" || mode == "Copy") ? _session.DestinationFolder : ""
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

        /// <summary>Loads persisted history into the collection at startup.</summary>
        public void LoadPersisted()
        {
            try
            {
                var history = _historyManager.LoadHistory();
                foreach (var entry in history)
                {
                    History.Add(entry);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading history: {ex.Message}");
            }
        }

        // Re-run a past operation (Tier 2): repopulate its settings for review, don't auto-execute.
        private void ReRunOperation(object parameter)
        {
            if (parameter is HistoryEntry entry)
            {
                if (!entry.CanReRun)
                {
                    _notify.SetStatus("This history item can't be re-run (no saved source/destination).");
                    return;
                }

                _session.SourceFolder = entry.SourceFolder;
                _session.DestinationFolder = entry.DestinationFolder;
                _session.OperationMode = entry.Mode == "Copy" ? FileOperationMode.Copy : FileOperationMode.Move;

                _notify.SetStatus($"Re-run ready: {entry.Mode} from \"{entry.SourceFolder}\". Open the Operations tab, scan, review, then run.");
            }
        }
    }
}
