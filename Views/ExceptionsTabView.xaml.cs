using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileOrganizer.Views
{
    /// <summary>
    /// Exceptions tab content. Inherits the MainWindow's DataContext (MainViewModel).
    /// The exception removal handler moved here with its DataGrid.
    /// </summary>
    public partial class ExceptionsTabView : UserControl
    {
        public ExceptionsTabView()
        {
            InitializeComponent();
        }

        private void RemoveSelectedExceptions_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = this.DataContext as ViewModels.MainViewModel;
            if (viewModel == null) return;

            var selectedItems = ExceptionsDataGrid.SelectedItems.Cast<Models.ExceptionFilter>().ToList();

            if (selectedItems.Count == 0)
            {
                viewModel.StatusMessage = "No exceptions selected to remove.";
                return;
            }

            foreach (var exception in selectedItems)
            {
                viewModel.Exceptions.Remove(exception);
            }

            viewModel.StatusMessage = $"Removed {selectedItems.Count} exception(s).";
        }
    }
}
