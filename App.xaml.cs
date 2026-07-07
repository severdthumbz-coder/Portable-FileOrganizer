using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace FileOrganizer
{
    public partial class App : Application
    {
        // AppUserModelID for toast notifications
        private const string AppId = "FileOrganizer.PortableFileOrganizer.v5";

        [DllImport("shell32.dll", SetLastError = true)]
        static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // CRITICAL: Set shutdown mode to prevent app from closing when splash closes
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // Register AppUserModelID for toast notifications
            try
            {
                SetCurrentProcessExplicitAppUserModelID(AppId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set AppUserModelID: {ex.Message}");
            }

            // Check if config exists (indicates not first launch)
            var configManager = new Services.ConfigManager();
            var configExists = System.IO.File.Exists(configManager.GetConfigPath());
            
            if (!configExists)
            {
                // First launch or reset - show splash screen
                ShowSplashScreen();
            }
            else
            {
                // Not first launch - skip splash and go straight to main window
                ShowMainWindow();
            }
        }

        private void ShowSplashScreen()
        {
            // CRITICAL FIX: Create MainWindow FIRST but don't show it yet
            // This prevents app from shutting down when splash closes
            var mainWindow = new MainWindow();
            this.MainWindow = mainWindow; // Set as main window so app doesn't close
            
            // Show splash screen
            var splash = new SplashScreen();
            splash.Show();
            
            // Animate progress bar over 2 seconds
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(20); // Update every 20ms
            int progress = 0;
            
            timer.Tick += (s, args) =>
            {
                progress += 1;
                splash.UpdateProgress(progress);
                
                if (progress >= 100)
                {
                    timer.Stop();
                    
                    // Close splash and show main window
                    splash.Close();
                    
                    // Now show the main window that was created earlier
                    mainWindow.Show();
                    
                    // Check for incomplete operations after window is shown
                    var viewModel = mainWindow.DataContext as ViewModels.MainViewModel;
                    if (viewModel != null)
                    {
                        // Use dispatcher to ensure UI is fully loaded
                        mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            viewModel.CheckForIncompleteOperation();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }
            };
            
            timer.Start();
        }

        private void ShowMainWindow()
        {
            var mainWindow = new MainWindow();
            this.MainWindow = mainWindow; // Set as main window
            mainWindow.Show();
            
            // Check for incomplete operations after window is shown
            var viewModel = mainWindow.DataContext as ViewModels.MainViewModel;
            if (viewModel != null)
            {
                // Use dispatcher to ensure UI is fully loaded
                mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    viewModel.CheckForIncompleteOperation();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// Apply theme by loading the appropriate ResourceDictionary
        /// </summary>
        /// <param name="isDarkMode">True for dark theme, false for light theme</param>
        public static void ApplyTheme(bool isDarkMode)
        {
            var app = Current as App;
            if (app == null) return;

            var dictionaries = app.Resources.MergedDictionaries;
            dictionaries.Clear();

            // Load the appropriate theme file
            var themeFile = isDarkMode ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
            var themeDict = new ResourceDictionary
            {
                Source = new Uri(themeFile, UriKind.Relative)
            };

            dictionaries.Add(themeDict);
        }
    }
}
