using System;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Concrete notification service.
    ///
    /// It owns the reference to the BannerNotification control (previously held by
    /// MainViewModel) and forwards status text through a callback supplied by whoever
    /// owns the status-bar property. That keeps this class free of any dependency on
    /// MainViewModel while still letting the status bar be a bindable property.
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly Action<string> _setStatus;
        private Controls.BannerNotification _banner;

        /// <param name="setStatus">Callback that assigns the bindable status-bar property.</param>
        public NotificationService(Action<string> setStatus)
        {
            _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        }

        /// <summary>
        /// Supplies the banner control once the window has been constructed.
        /// Until this is called, banner requests are silently ignored (as before).
        /// </summary>
        public void AttachBanner(Controls.BannerNotification banner) => _banner = banner;

        public void SetStatus(string message) => _setStatus(message);

        public void ShowCompletionBanner(string operation, string statistics, string icon = "✅")
        {
            if (_banner == null) return;

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _banner.Show(
                    title: $"{operation.ToUpper()} COMPLETE!",
                    message: statistics,
                    icon: icon,
                    autoDismissSeconds: 15  // Auto-dismiss after 15 seconds
                );
            });
        }
    }
}
