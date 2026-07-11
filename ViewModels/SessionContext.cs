using System.Collections.ObjectModel;
using FileOrganizer.Models;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// The shared "what are we operating on" state: the source and destination folders,
    /// the additional source folders, and whether we're moving or copying.
    ///
    /// Before this existed, every feature reached into MainViewModel for these values
    /// (SourceFolder had 41 references, DestinationFolder 28). Centralising them here lets
    /// feature ViewModels take a SessionContext instead of the God object, and gives one
    /// obvious place for this state to live.
    ///
    /// This is observable so views can bind two-way exactly as they did before.
    /// </summary>
    public class SessionContext : ViewModelBase
    {
        private string _sourceFolder = string.Empty;
        public string SourceFolder
        {
            get => _sourceFolder;
            set
            {
                if (SetProperty(ref _sourceFolder, value))
                {
                    // Storage-detection side effect lives here so that EVERY writer of
                    // SourceFolder (the Configuration tab, a History Re-run, etc.) triggers
                    // capability refresh — not just the MainViewModel property setter.
                    if (!string.IsNullOrEmpty(value) && System.IO.Directory.Exists(value))
                    {
                        Services.AdaptivePerformanceManager.Instance.RefreshCapabilities(value);
                    }
                    SourceFolderChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Raised after SourceFolder changes (and capabilities are refreshed) so an owner can
        /// re-raise dependent UI notifications such as storage-detection text.
        /// </summary>
        public event System.Action SourceFolderChanged;

        private string _destinationFolder = string.Empty;
        public string DestinationFolder
        {
            get => _destinationFolder;
            set => SetProperty(ref _destinationFolder, value);
        }

        private FileOperationMode _operationMode = FileOperationMode.Move;
        public FileOperationMode OperationMode
        {
            get => _operationMode;
            set => SetProperty(ref _operationMode, value);
        }

        private bool _useMultipleSources;
        public bool UseMultipleSources
        {
            get => _useMultipleSources;
            set => SetProperty(ref _useMultipleSources, value);
        }

        /// <summary>Additional source folders when UseMultipleSources is enabled.</summary>
        public ObservableCollection<string> SourceFolders { get; } = new ObservableCollection<string>();
    }
}
