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
    }
}
