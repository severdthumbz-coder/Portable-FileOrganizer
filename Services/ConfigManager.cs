using System;
using System.IO;
using Newtonsoft.Json;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Manages saving and loading application configuration
    /// </summary>
    public class ConfigManager
    {
        private readonly string _configPath;
        private const string ConfigFileName = "config.json";

        public ConfigManager()
        {
            // Store config in user's AppData folder
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PortableFileOrganizer"
            );

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _configPath = Path.Combine(appDataPath, ConfigFileName);
        }

        public ConfigManager(string customPath)
        {
            _configPath = customPath;
        }

        /// <summary>
        /// Saves configuration to JSON file
        /// </summary>
        public bool SaveConfig(Config config)
        {
            try
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads configuration from JSON file
        /// </summary>
        public Config LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    return CreateDefaultConfig();
                }

                var json = File.ReadAllText(_configPath);
                var config = JsonConvert.DeserializeObject<Config>(json);
                return config ?? CreateDefaultConfig();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
                return CreateDefaultConfig();
            }
        }

        /// <summary>
        /// Creates default configuration
        /// </summary>
        private Config CreateDefaultConfig()
        {
            return new Config
            {
                ScanMode = ScanMode.Auto,
                CopyEngine = CopyEngine.CustomFast,
                OperationMode = FileOperationMode.Move,
                StructureMode = DestinationStructureMode.PreserveStructure,
                ConflictResolution = FileConflictResolution.Skip,
                ContinueOnErrors = true,
                RetryAttempts = 3,
                RetryDelaySeconds = 2
            };
        }

        /// <summary>
        /// Checks if configuration file exists
        /// </summary>
        public bool ConfigExists()
        {
            return File.Exists(_configPath);
        }

        /// <summary>
        /// Deletes configuration file
        /// </summary>
        public bool ClearConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    File.Delete(_configPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing config: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the full path to the configuration file
        /// </summary>
        public string GetConfigPath()
        {
            return _configPath;
        }

        /// <summary>
        /// Reads only the two onboarding flags (Build 1.5.0). Used by the OnboardingKit store so
        /// its state lives in the one config.json without routing through the main Save/Load
        /// orchestration. Missing file or fields fall back to defaults (helpers ON, tour unseen).
        /// </summary>
        public (bool ShowOnboardingHelpers, bool HasSeenTour) LoadOnboardingFlags()
        {
            try
            {
                if (!File.Exists(_configPath))
                    return (true, false);

                var json = File.ReadAllText(_configPath);
                var config = JsonConvert.DeserializeObject<Config>(json);
                return config == null
                    ? (true, false)
                    : (config.ShowOnboardingHelpers, config.HasSeenTour);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading onboarding flags: {ex.Message}");
                return (true, false);
            }
        }

        /// <summary>
        /// Writes only the two onboarding flags (Build 1.5.0) via read-modify-write, so toggling
        /// the "Show onboarding helpers" checkbox persists immediately without rebuilding — and
        /// cannot clobber unsaved Configuration-tab edits. Creates the file from defaults if absent.
        /// </summary>
        public bool SaveOnboardingFlags(bool showOnboardingHelpers, bool hasSeenTour)
        {
            try
            {
                var config = LoadOrDefault();
                config.ShowOnboardingHelpers = showOnboardingHelpers;
                config.HasSeenTour = hasSeenTour;
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(config, Formatting.Indented));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving onboarding flags: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads only the two tray flags (Build 1.5.0). Missing file/fields fall back to OFF/OFF.
        /// </summary>
        public (bool MinimizeToTray, bool CloseToTray) LoadTrayFlags()
        {
            try
            {
                if (!File.Exists(_configPath))
                    return (false, false);

                var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(_configPath));
                return config == null ? (false, false) : (config.MinimizeToTray, config.CloseToTray);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tray flags: {ex.Message}");
                return (false, false);
            }
        }

        /// <summary>
        /// Writes only the two tray flags (Build 1.5.0) via read-modify-write, so toggling a tray
        /// setting persists immediately without clobbering unsaved Configuration-tab edits.
        /// </summary>
        public bool SaveTrayFlags(bool minimizeToTray, bool closeToTray)
        {
            try
            {
                var config = LoadOrDefault();
                config.MinimizeToTray = minimizeToTray;
                config.CloseToTray = closeToTray;
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(config, Formatting.Indented));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving tray flags: {ex.Message}");
                return false;
            }
        }

        /// <summary>Loads the existing config for a partial-field update, or a default if absent/corrupt.</summary>
        private Config LoadOrDefault()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonConvert.DeserializeObject<Config>(json) ?? CreateDefaultConfig();
            }
            return CreateDefaultConfig();
        }
    }
}
