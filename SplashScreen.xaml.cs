using System.Windows;

namespace FileOrganizer
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
        }

        public void UpdateProgress(int value)
        {
            LoadingProgress.Value = value;
        }
    }
}
