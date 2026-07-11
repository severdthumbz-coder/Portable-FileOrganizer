using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using FileOrganizer.Commands;
using FileOrganizer.Models;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Owns the Exceptions tab: the list of exception filters and the add/remove UI.
    ///
    /// The exception *filtering logic* (ApplyExceptionFilters) stays in the scan pipeline
    /// in MainViewModel, which reads this collection. So this ViewModel owns the collection
    /// and the UI actions; MainViewModel exposes the same collection instance to the pipeline.
    ///
    /// Depends only on INotificationService (status) and SessionContext (to seed folder
    /// dialogs from the current source folder).
    /// </summary>
    public class ExceptionsViewModel : ViewModelBase
    {
        private readonly INotificationService _notify;
        private readonly SessionContext _session;

        public ExceptionsViewModel(INotificationService notify, SessionContext session)
        {
            _notify = notify ?? throw new ArgumentNullException(nameof(notify));
            _session = session ?? throw new ArgumentNullException(nameof(session));

            AddExceptionCommand = new RelayCommand(_ => AddException());
            RemoveExceptionCommand = new RelayCommand(RemoveException);
        }

        /// <summary>
        /// The exception filters. This exact instance is also exposed by MainViewModel so the
        /// scan pipeline can apply them.
        /// </summary>
        public ObservableCollection<ExceptionFilter> Exceptions { get; } = new ObservableCollection<ExceptionFilter>();

        public ICommand AddExceptionCommand { get; }
        public ICommand RemoveExceptionCommand { get; }

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

            var sourceFolder = _session.SourceFolder;

            if (isFolder)
            {
                // Show folder browser
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = "Select folder to add as exception from source";

                if (!string.IsNullOrEmpty(sourceFolder) && System.IO.Directory.Exists(sourceFolder))
                {
                    folderDialog.SelectedPath = sourceFolder;
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

                if (!string.IsNullOrEmpty(sourceFolder) && System.IO.Directory.Exists(sourceFolder))
                {
                    fileDialog.InitialDirectory = sourceFolder;
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
                _notify.SetStatus($"Exception added: {typeName} ({excTypeName})");

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
                    _notify.SetStatus("Exception removed");
                }
            }
        }

        /// <summary>
        /// Removes a set of exceptions (used by the tab's multi-select "Remove Selected"
        /// button, which is wired in code-behind because it reads the DataGrid selection).
        /// </summary>
        public void RemoveSelected(System.Collections.Generic.IEnumerable<ExceptionFilter> filters)
        {
            int count = 0;
            foreach (var f in filters)
            {
                if (Exceptions.Remove(f)) count++;
            }
            _notify.SetStatus(count == 0
                ? "No exceptions selected to remove."
                : $"Removed {count} exception(s).");
        }
    }
}
