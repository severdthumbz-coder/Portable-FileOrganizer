namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// A place for a feature ViewModel to report a one-line status message.
    ///
    /// Child ViewModels (Search, Automation, …) depend on this interface rather than on
    /// MainViewModel, so they can be constructed, tested, and reasoned about in isolation.
    /// MainViewModel implements it by forwarding to its StatusMessage property, which the
    /// status bar binds to.
    /// </summary>
    public interface IStatusSink
    {
        /// <summary>Sets the application's status line.</summary>
        void SetStatus(string message);
    }
}
