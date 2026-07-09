using System.Collections.Generic;
using FileOrganizer.Models;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Supplies the shared transfer settings and folder context that a feature ViewModel
    /// needs in order to build a MoveEngine, without depending on MainViewModel itself.
    ///
    /// This exists because the automation features (watcher, scheduler) must honour the
    /// same verification / retry / timestamp settings the user configured on the
    /// Configuration tab, but should not reach back into the God object to read them.
    /// </summary>
    public interface ITransferSettingsProvider
    {
        bool PreserveTimestamps { get; }
        VerificationMode VerificationMode { get; }
        bool VerifyExternalCopies { get; }
        int RetryAttempts { get; }
        int RetryDelaySeconds { get; }

        /// <summary>Configured source folders, used as a fallback when no watch folders are set.</summary>
        IEnumerable<string> SourceFolders { get; }

        /// <summary>Configured destination folder, used as a fallback search root.</summary>
        string DestinationFolder { get; }

        /// <summary>
        /// Builds a Config carrying the current transfer settings. Automation deliberately
        /// pins CopyEngine to CustomFast: it is the safe, silent, verified engine.
        /// </summary>
        Config BuildAutomationConfig();
    }
}
