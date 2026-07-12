using FileOrganizer.Models;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// The write channel for the statistics read-model (the counters shown on the Statistics
    /// tab). Operational features — the Operations pipeline and the Duplicates tab — update
    /// statistics through this interface rather than by touching StatisticsViewModel directly.
    ///
    /// StatisticsViewModel implements it. MainViewModel forwards to that instance so the
    /// operational code in MainViewModel can keep calling a local _stats reference.
    /// </summary>
    public interface IStatsSink
    {
        // Duplicate-detection results (written by DuplicatesViewModel).
        int DuplicateGroupsFound { get; set; }
        double WastedSpaceGB { get; set; }

        // Operation counters (written by the Operations pipeline).
        int TotalFilesOrganized { get; set; }
        int TotalOperations { get; set; }
        double DataProcessedGB { get; set; }

        // Verification counters (written by the Operations pipeline).
        int TotalFilesVerified { get; set; }
        int VerificationPassed { get; set; }
        int VerificationFailed { get; set; }
        int VerificationRetried { get; set; }

        /// <summary>Increments the total operation count by one.</summary>
        void IncrementOperations();

        /// <summary>Records the outcome of a completed batch operation in one call.</summary>
        void RecordOperation(int filesOrganized, double dataProcessedGB,
            int filesVerified, int verificationPassed, int verificationFailed, int verificationRetried);

        /// <summary>Logs a single verification result.</summary>
        void LogVerification(VerificationLog log);
    }
}
