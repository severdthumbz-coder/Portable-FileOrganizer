using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WinForms = System.Windows.Forms;
using FileOrganizer.ViewModels;

namespace FileOrganizer.Tray
{
    /// <summary>
    /// Owns the system-tray presence (Build 1.5.0). Uses System.Windows.Forms.NotifyIcon
    /// (WinForms is already referenced via &lt;UseWindowsForms&gt;true), so no new NuGet package.
    ///
    /// Responsibilities:
    ///  - Show a tray icon with an automation-aware right-click menu.
    ///  - Minimize-to-tray and close-to-tray, each gated by a user setting (Config).
    ///  - Restore the window on double-click or "Open".
    ///  - "Exit" always truly quits (tears down the icon, then shuts the app down).
    ///
    /// The window's own StateChanged/Closing handlers call into this manager; it does not
    /// subscribe to them itself, so the window stays in control of its lifecycle.
    /// </summary>
    public sealed class TrayIconManager : IDisposable
    {
        private readonly Window _window;
        private readonly MainViewModel _vm;
        private readonly Func<bool> _getMinimizeToTray;
        private readonly Func<bool> _getCloseToTray;

        private WinForms.NotifyIcon _notifyIcon;
        private bool _reallyExit;      // set by Exit so the Closing handler lets the app quit
        private bool _disposed;

        public TrayIconManager(Window window, MainViewModel vm,
                               Func<bool> getMinimizeToTray, Func<bool> getCloseToTray)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _getMinimizeToTray = getMinimizeToTray ?? (() => false);
            _getCloseToTray = getCloseToTray ?? (() => false);

            BuildNotifyIcon();
        }

        /// <summary>True once "Exit" has been chosen, so the window's Closing handler lets it close.</summary>
        public bool ReallyExit => _reallyExit;

        private void BuildNotifyIcon()
        {
            _notifyIcon = new WinForms.NotifyIcon
            {
                Text = "Portable File Organizer",
                Visible = true,
                Icon = LoadTrayIcon()
            };

            // Double-click (or double-left-click) restores the window.
            _notifyIcon.DoubleClick += (_, _) => ShowWindow();

            // Rebuild the menu each time it opens so Start/Stop labels reflect live state.
            var menu = new WinForms.ContextMenuStrip();
            menu.Opening += (_, _) => PopulateMenu(menu);
            _notifyIcon.ContextMenuStrip = menu;
        }

        private System.Drawing.Icon LoadTrayIcon()
        {
            // The app icon is embedded as the EXE's icon (ApplicationIcon in the csproj); pull it
            // from the running executable. Fall back to the system application icon if that fails.
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null) return icon;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tray icon load failed: {ex.Message}");
            }
            return System.Drawing.SystemIcons.Application;
        }

        private void PopulateMenu(WinForms.ContextMenuStrip menu)
        {
            menu.Items.Clear();
            var automation = _vm.Automation;

            menu.Items.Add(MakeItem("Open", (_, _) => ShowWindow()));
            menu.Items.Add(new WinForms.ToolStripSeparator());

            menu.Items.Add(MakeItem("Run Sweep Now", (_, _) =>
                InvokeCommand(automation.RunSweepNowCommand)));

            // Watching: toggle label by live state.
            if (automation.IsWatching)
                menu.Items.Add(MakeItem("Stop Watching", (_, _) => InvokeCommand(automation.StopWatchingCommand)));
            else
                menu.Items.Add(MakeItem("Start Watching", (_, _) => InvokeCommand(automation.StartWatchingCommand)));

            // Scheduler: toggle label by live state.
            if (automation.IsScheduleRunning)
                menu.Items.Add(MakeItem("Pause Scheduler", (_, _) => InvokeCommand(automation.StopScheduleCommand)));
            else
                menu.Items.Add(MakeItem("Start Scheduler", (_, _) => InvokeCommand(automation.StartScheduleCommand)));

            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add(MakeItem("Exit", (_, _) => ExitApplication()));
        }

        private static WinForms.ToolStripMenuItem MakeItem(string text, EventHandler onClick)
        {
            var item = new WinForms.ToolStripMenuItem(text);
            item.Click += onClick;
            return item;
        }

        private void InvokeCommand(ICommand command)
        {
            // Menu clicks arrive on the UI thread (WinForms message loop shares it with WPF here),
            // but marshal through the dispatcher to be safe, and only fire if CanExecute allows.
            _window.Dispatcher.Invoke(() =>
            {
                if (command != null && command.CanExecute(null))
                    command.Execute(null);
            });
        }

        // ───────────────────────── window lifecycle ─────────────────────────

        /// <summary>Call from MainWindow.StateChanged. Returns true if it hid the window to the tray.</summary>
        public bool OnStateChanged()
        {
            if (_getMinimizeToTray() && _window.WindowState == WindowState.Minimized)
            {
                HideWindow();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Call from MainWindow.Closing. When close-to-tray is on and this isn't a real exit,
        /// cancels the close and hides to the tray instead. Returns true if the close was handled
        /// (i.e. the caller should set e.Cancel = true).
        /// </summary>
        public bool OnClosing()
        {
            if (!_reallyExit && _getCloseToTray())
            {
                HideWindow();
                return true;   // caller cancels the close
            }
            return false;      // let the window close normally
        }

        public void ShowWindow()
        {
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.ShowInTaskbar = true;
            _window.Activate();
            _window.Topmost = true;    // bump to front…
            _window.Topmost = false;   // …without staying pinned
            _window.Focus();
        }

        private void HideWindow()
        {
            _window.Hide();
            _window.ShowInTaskbar = false;
        }

        private void ExitApplication()
        {
            _reallyExit = true;
            // Remove the icon before shutting down so it doesn't linger in the tray.
            Dispose();
            _window.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
