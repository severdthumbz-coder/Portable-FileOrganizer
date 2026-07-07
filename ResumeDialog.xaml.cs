using System.Windows;
using FileOrganizer.Services;

namespace FileOrganizer
{
    /// <summary>
    /// Interaction logic for ResumeDialog.xaml
    /// </summary>
    public partial class ResumeDialog : Window
    {
        public enum ResumeAction
        {
            Resume,
            Undo,
            Cancel
        }

        public ResumeAction SelectedAction { get; private set; }

        public ResumeDialog(ResumeSummary summary)
        {
            InitializeComponent();

            if (summary != null)
            {
                // Set operation details
                OperationModeText.Text = summary.OperationMode;
                InterruptedTimeText.Text = $"Interrupted {summary.TimeSinceInterruption}";
                
                // Set progress
                ProgressText.Text = $"{summary.CompletedFiles:N0} of {summary.TotalFiles:N0} files completed ({summary.PercentComplete:F1}%)";
                ProgressBar.Value = summary.PercentComplete;
                
                // Set folder paths
                SourceFolderText.Text = summary.SourceFolder ?? "Unknown";
                DestinationFolderText.Text = summary.DestinationFolder ?? "Unknown";
                
                // Set remaining files
                RemainingFilesText.Text = $"{summary.RemainingFiles:N0} file{(summary.RemainingFiles != 1 ? "s" : "")}";
            }

            SelectedAction = ResumeAction.Cancel; // Default
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = ResumeAction.Resume;
            DialogResult = true;
            Close();
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will reverse all files that were already moved/copied during the incomplete operation.\n\nAre you sure you want to undo?",
                "Confirm Undo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SelectedAction = ResumeAction.Undo;
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will discard the incomplete operation state. You will not be able to resume or undo.\n\nAre you sure you want to cancel?",
                "Confirm Cancel",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                SelectedAction = ResumeAction.Cancel;
                DialogResult = false;
                Close();
            }
        }
    }
}
