namespace FileOrganizer.ViewModels
{
    /// <summary>
    /// Lets ConfigurationViewModel trigger the Save/Clear actions whose buttons live on the
    /// Configuration tab, without ConfigurationViewModel owning the cross-tab orchestration.
    ///
    /// Save/Clear are NOT pure Configuration-tab concerns: SaveConfig() reads Automation,
    /// Exceptions and SourceFolders as well as the Config settings block, and both write the
    /// persisted file. That orchestration stays in MainViewModel (which implements this
    /// interface); ConfigurationViewModel's SaveConfigCommand/ClearConfigCommand are thin
    /// forwarders to it, mirroring how OperationsViewModel receives IOperationsSettingsProvider.
    ///
    /// The settings-block detail still lives on ConfigurationViewModel via BuildConfig(Config)
    /// and ApplyConfig(Config), which MainViewModel's SaveConfig()/LoadPersistedData() call.
    /// </summary>
    public interface IConfigPersistence
    {
        void SaveConfig();
        void ClearConfig();
    }
}
