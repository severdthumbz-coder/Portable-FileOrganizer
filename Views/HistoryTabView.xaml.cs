using System.Windows.Controls;

namespace FileOrganizer.Views
{
    /// <summary>
    /// History tab content. Inherits the MainWindow's DataContext (MainViewModel),
    /// so all bindings resolve exactly as they did when this markup lived in MainWindow.xaml.
    /// </summary>
    public partial class HistoryTabView : UserControl
    {
        public HistoryTabView()
        {
            InitializeComponent();
        }
    }
}
