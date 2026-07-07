using System;
using System.IO;
using Microsoft.Win32;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Detects installed copy engines (TeraCopy, FastCopy) on the system
    /// </summary>
    public class EngineDetector
    {
        /// <summary>
        /// Result of an engine detection check
        /// </summary>
        public class DetectionResult
        {
            public bool IsInstalled { get; set; }
            public string InstallPath { get; set; }
            public string Version { get; set; }
            public string Message { get; set; }
        }

        /// <summary>
        /// Detects TeraCopy installation
        /// </summary>
        public static DetectionResult DetectTeraCopy()
        {
            var result = new DetectionResult
            {
                IsInstalled = false,
                InstallPath = string.Empty,
                Version = "Unknown",
                Message = "TeraCopy not detected"
            };

            try
            {
                // Check common installation paths
                string[] commonPaths = new[]
                {
                    @"C:\Program Files\TeraCopy\TeraCopy.exe",
                    @"C:\Program Files (x86)\TeraCopy\TeraCopy.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"TeraCopy\TeraCopy.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"TeraCopy\TeraCopy.exe")
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        result.IsInstalled = true;
                        result.InstallPath = path;
                        result.Message = $"TeraCopy found at: {path}";
                        
                        // Try to get version from file
                        try
                        {
                            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                            result.Version = versionInfo.FileVersion ?? "Unknown";
                        }
                        catch
                        {
                            result.Version = "Unable to determine version";
                        }
                        
                        return result;
                    }
                }

                // Check Windows Registry
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Code Sector\TeraCopy"))
                    {
                        if (key != null)
                        {
                            var installPath = key.GetValue("InstallPath") as string;
                            if (!string.IsNullOrEmpty(installPath))
                            {
                                var exePath = Path.Combine(installPath, "TeraCopy.exe");
                                if (File.Exists(exePath))
                                {
                                    result.IsInstalled = true;
                                    result.InstallPath = exePath;
                                    result.Message = $"TeraCopy found via registry: {exePath}";
                                    
                                    try
                                    {
                                        var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                                        result.Version = versionInfo.FileVersion ?? "Unknown";
                                    }
                                    catch { }
                                    
                                    return result;
                                }
                            }
                        }
                    }
                }
                catch { }

                result.Message = "TeraCopy not found. Please install TeraCopy or use a different engine.";
            }
            catch (Exception ex)
            {
                result.Message = $"Error detecting TeraCopy: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Detects FastCopy installation
        /// </summary>
        public static DetectionResult DetectFastCopy()
        {
            var result = new DetectionResult
            {
                IsInstalled = false,
                InstallPath = string.Empty,
                Version = "Unknown",
                Message = "FastCopy not detected"
            };

            try
            {
                // Check common installation paths
                string[] commonPaths = new[]
                {
                    @"C:\Program Files\FastCopy\FastCopy.exe",
                    @"C:\Program Files (x86)\FastCopy\FastCopy.exe",
                    @"C:\FastCopy\FastCopy.exe",
                    @"C:\Users\ragin\FastCopy\FastCopy.exe",  // User's custom location
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"FastCopy\FastCopy.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"FastCopy\FastCopy.exe")
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        result.IsInstalled = true;
                        result.InstallPath = path;
                        result.Message = $"FastCopy found at: {path}";
                        
                        // Try to get version from file
                        try
                        {
                            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                            result.Version = versionInfo.FileVersion ?? "Unknown";
                        }
                        catch
                        {
                            result.Version = "Unable to determine version";
                        }
                        
                        return result;
                    }
                }

                // Check for portable version in common locations
                string[] portablePaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Downloads\FastCopy\FastCopy.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"FastCopy\FastCopy.exe")
                };

                foreach (var path in portablePaths)
                {
                    if (File.Exists(path))
                    {
                        result.IsInstalled = true;
                        result.InstallPath = path;
                        result.Message = $"FastCopy (portable) found at: {path}";
                        
                        try
                        {
                            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                            result.Version = versionInfo.FileVersion ?? "Unknown";
                        }
                        catch { }
                        
                        return result;
                    }
                }

                result.Message = "FastCopy not found. Please install FastCopy or use a different engine.";
            }
            catch (Exception ex)
            {
                result.Message = $"Error detecting FastCopy: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Gets a user-friendly status string for display
        /// </summary>
        public static string GetEngineStatus(DetectionResult result)
        {
            if (result.IsInstalled)
            {
                return $"✓ Installed - Version {result.Version}\nLocation: {result.InstallPath}";
            }
            else
            {
                return $"✗ Not Installed\n{result.Message}";
            }
        }
    }
}
