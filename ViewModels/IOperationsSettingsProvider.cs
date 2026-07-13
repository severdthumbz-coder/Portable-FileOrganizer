using System.Collections.Generic;
using FileOrganizer.Models;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Supplies the Configuration-tab settings that the Operations pipeline needs when it
    /// builds a MoveEngine or previews destination paths, without OperationsViewModel
    /// depending on MainViewModel.
    ///
    /// These are read-only from the Operations tab's perspective: the user sets them on the
    /// Configuration tab; Operations honours them. Folder state and operation mode come from
    /// SessionContext; this covers the remaining scan/structure/conflict settings and the
    /// exception filter, which still lives with the scan pipeline in MainViewModel.
    /// </summary>
    public interface IOperationsSettingsProvider
    {
        ScanMode SelectedScanMode { get; }
        DestinationStructureMode StructureMode { get; }
        FileConflictResolution ConflictResolution { get; }

        /// <summary>Applies the user's exception filters to a freshly scanned set of entries.</summary>
        List<QueueEntry> ApplyExceptionFilters(List<QueueEntry> entries);
    }
}
