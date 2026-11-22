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

        // ---- Single-instance (Build 1.5.0) ----
        // A named Mutex detects an already-running instance; a named EventWaitHandle lets a
        // second launch signal the first to re-show its window. Names are per-user so two
        // different users on the same machine each get their own instance.
        private const string SingleInstanceMutexName = "FileOrganizer.PortableFileOrganizer.v5.Instance";
        private const string ShowWindowEventName = "FileOrganizer.PortableFileOrganizer.v5.ShowWindow";
        private Mutex _singleInstanceMutex;
        private EventWaitHandle _showWindowEvent;

        [DllImport("shell32.dll", SetLastError = true)]
        static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Single-instance gate: if another instance already holds the mutex, signal it to
            // show its window and exit this one immediately.
            var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool isFirstInstance);
            if (!isFirstInstance)
            {
                // Not the first instance: we never owned the mutex, so just dispose the handle
                // (no ReleaseMutex), signal the running instance to surface, and exit.
                mutex.Dispose();
                try
                {
                    if (EventWaitHandle.TryOpenExisting(ShowWindowEventName, out var existing))
                    {
                        existing.Set();
                        existing.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to signal existing instance: {ex.Message}");
                }
                Shutdown();
                return;
            }

            // We own the mutex — keep the reference for release on exit.
            _singleInstanceMutex = mutex;

            // We are the first instance. Create the signal other instances will set, and listen
            // for it on a background thread to re-show our window.
            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
            RegisterSecondInstanceListener();

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

            // ---- Onboarding (Build 1.5.0) ----
            // Wire the OnboardingKit to the two flags in config.json. The flags are held in a
            // small in-memory pair (seeded from disk), the kit's setters update it, and Save()
            // does one read-modify-write of only those two fields — so toggling the helper
            // checkbox persists immediately without touching the rest of the config, and can't
            // clobber unsaved Configuration-tab edits. The tab selector is set later by
            // MainWindow, which owns the TabControl.
            var flags = configManager.LoadOnboardingFlags();
            bool showHelpers = flags.ShowOnboardingHelpers;
            bool hasSeenTour = flags.HasSeenTour;
            var onboardingStore = new OnboardingKit.DelegateOnboardingStore(
                getHelpers: () => showHelpers,
                setHelpers: v => showHelpers = v,
                getSeen: () => hasSeenTour,
                setSeen: v => hasSeenTour = v,
                save: () => configManager.SaveOnboardingFlags(showHelpers, hasSeenTour));
            OnboardingKit.OnboardingController.Current.Initialize(
                onboardingStore,
                OnboardingContent.TourSteps,
                OnboardingContent.LegendItems);

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

        /// <summary>
        /// Background wait on the "show window" event. When a second instance sets it, bring this
        /// instance's main window back to the front on the UI thread, then keep listening.
        /// </summary>
        private void RegisterSecondInstanceListener()
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        _showWindowEvent.WaitOne();
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var window = MainWindow;
                            if (window != null)
                            {
                                window.Show();
                                window.WindowState = WindowState.Normal;
                                window.ShowInTaskbar = true;
                                window.Activate();
                                window.Topmost = true;
                                window.Topmost = false;
                                window.Focus();
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Second-instance listener error: {ex.Message}");
                        break;
                    }
                }
            })
            {
                IsBackground = true,
                Name = "SingleInstanceListener"
            };
            thread.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Release single-instance handles so the next launch starts cleanly.
            try { _showWindowEvent?.Dispose(); } catch { }
            try
            {
                if (_singleInstanceMutex != null)
                {
                    _singleInstanceMutex.ReleaseMutex();
                    _singleInstanceMutex.Dispose();
                }
            }
            catch { /* mutex may already be released if we never owned it */ }

            base.OnExit(e);
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

            // Build the new theme dictionary.
            var themeFile = isDarkMode ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
            var themeDict = new ResourceDictionary
            {
                Source = new Uri(themeFile, UriKind.Relative)
            };

            // Replace ONLY the theme dictionary, preserving every other merged dictionary
            // (notably Onboarding/Onboarding.xaml, Build 1.5.0). Previously this cleared all
            // merged dictionaries, which would have dropped the OnboardingKit styles on a theme
            // switch. A theme dictionary is identified by its Source path under "Themes/".
            for (int i = dictionaries.Count - 1; i >= 0; i--)
            {
                var src = dictionaries[i].Source?.OriginalString ?? string.Empty;
                if (src.Contains("Themes/", StringComparison.OrdinalIgnoreCase))
                {
                    dictionaries.RemoveAt(i);
                }
            }

            // Theme goes last so its Onb* aliases override the kit's defaults in Onboarding.xaml.
            dictionaries.Add(themeDict);
        }
    }
}
