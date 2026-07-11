namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// A write-only channel for updating the statistics read-model (the counters shown on
    /// the Statistics tab).
    ///
    /// The Statistics tab is a passive display of totals that operational features increment.
    /// Those counters still live on MainViewModel for now (they are written from many places
    /// across the Operations pipeline and will be consolidated in a later step). This
    /// interface lets an extracted feature — the Duplicates tab — update them without
    /// depending on MainViewModel directly.
    /// </summary>
    public interface IStatsSink
    {
        int DuplicateGroupsFound { get; set; }
        double WastedSpaceGB { get; set; }

        /// <summary>Increments the total operation count by one.</summary>
        void IncrementOperations();
    }
}
