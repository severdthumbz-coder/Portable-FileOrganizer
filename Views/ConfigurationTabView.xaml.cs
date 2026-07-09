using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileOrganizer.Views
{
    /// <summary>
    /// Configuration tab content. Inherits the MainWindow's DataContext (MainViewModel).
    /// The source-folder removal handler moved here with its DataGrid.
    /// </summary>
    public partial class ConfigurationTabView : UserControl
    {
        public ConfigurationTabView()
        {
            InitializeComponent();
        }

        private void RemoveSelectedSourceFolders_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = this.DataContext as ViewModels.MainViewModel;
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
