using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileOrganizer.Views
{
    /// <summary>
    /// Exceptions tab content. Its DataContext is an ExceptionsViewModel (set in MainWindow.xaml).
    /// The multi-select removal handler reads the DataGrid selection, so it stays in code-behind
    /// and delegates the actual work to the ViewModel.
    /// </summary>
    public partial class ExceptionsTabView : UserControl
    {
        public ExceptionsTabView()
        {
            InitializeComponent();
        }

        private void RemoveSelectedExceptions_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ViewModels.ExceptionsViewModel vm)
            {
                var selected = ExceptionsDataGrid.SelectedItems.Cast<Models.ExceptionFilter>().ToList();
                vm.RemoveSelected(selected);
            }
        }
    }
}
