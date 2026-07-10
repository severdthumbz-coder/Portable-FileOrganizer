namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// The single channel through which any feature tells the user something:
    /// the status line at the bottom of the window, and the in-app completion banner.
    ///
    /// Grouping these is deliberate. They are the same concern ("report to the user"),
    /// and keeping them behind one interface means a feature ViewModel never needs a
    /// reference to MainViewModel or to the BannerNotification control.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>Sets the one-line status message shown in the status bar.</summary>
        void SetStatus(string message);

        /// <summary>
        /// Shows the in-app completion banner. Non-blocking; auto-dismisses.
        /// </summary>
        /// <param name="operation">Operation name, rendered uppercase (e.g. "Move" → "MOVE COMPLETE!").</param>
        /// <param name="statistics">Detail line beneath the title.</param>
        /// <param name="icon">Emoji icon for the banner.</param>
        void ShowCompletionBanner(string operation, string statistics, string icon = "✅");
    }
}
