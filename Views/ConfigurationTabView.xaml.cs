using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileOrganizer.Views
{
    /// <summary>
    /// Configuration tab content. DataContext is ConfigurationViewModel (Build 1.4.9).
    /// The source-folder removal handler targets that VM's session-backed SourceFolders.
    /// </summary>
    public partial class ConfigurationTabView : UserControl
    {
        public ConfigurationTabView()
        {
            InitializeComponent();
        }

        private void RemoveSelectedSourceFolders_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = this.DataContext as ViewModels.ConfigurationViewModel;
            if (viewModel == null) return;

            var selectedItems = SourceFoldersDataGrid.SelectedItems.Cast<string>().ToList();

            if (selectedItems.Count == 0)
            {
                viewModel.StatusMessage = "No source folders selected to remove.";
                return;
            }

            foreach (var folder in selectedItems)
            {
                viewModel.SourceFolders.Remove(folder);
            }

            viewModel.StatusMessage = $"Removed {selectedItems.Count} source folder(s).";
        }
    }
}
