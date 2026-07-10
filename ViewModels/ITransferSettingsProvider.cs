using FileOrganizer.Models;

namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Supplies the shared transfer settings a feature ViewModel needs in order to build a
    /// MoveEngine, without depending on MainViewModel itself.
    ///
    /// Folder state is NOT here — that lives in SessionContext. This interface is only
    /// about *how* files are transferred (verification, retries, timestamps), not *what*
    /// they are or *where* they go.
    /// </summary>
    public interface ITransferSettingsProvider
    {
        /// <summary>
        /// Builds a Config carrying the current transfer settings. Automation deliberately
        /// pins CopyEngine to CustomFast: it is the safe, silent, verified engine.
        /// </summary>
        Config BuildAutomationConfig();
    }
}
