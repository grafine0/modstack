using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModStack.Core.Services;

namespace ModStack.GUI.Services
{
    /// <summary>
    /// Service for Apache-specific configuration settings
    /// </summary>
    public class ApacheConfigurationService
    {
        private readonly ISettingsService _settingsService;
        private const string DefaultApacheVersion = "2.4.65";

        public ApacheConfigurationService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>
        /// Gets the configured Apache version, falling back to default if not set
        /// </summary>
        public string GetApacheVersion()
        {
            try
            {
                // Try to detect from installation path
                var detectedVersion = DetectApacheVersion();
                if (!string.IsNullOrWhiteSpace(detectedVersion))
                {
                    return detectedVersion;
                }

                // Fall back to default
                return DefaultApacheVersion;
            }
            catch
            {
                return DefaultApacheVersion;
            }
        }

        /// <summary>
        /// Attempts to detect Apache version from installation
        /// </summary>
        private string? DetectApacheVersion()
        {
            var possiblePaths = new[]
            {
                Path.Combine(_settingsService.InstallPath ?? @"C:\ModStack\Services", "Apache"),
                @"C:\ModStack\Services\Apache",
                @"C:\xampp\apache",
                @"C:\wamp\bin\apache"
            };

            foreach (var basePath in possiblePaths)
            {
                if (Directory.Exists(basePath))
                {
                    var directories = Directory.GetDirectories(basePath)
                        .Where(d => Path.GetFileName(d).StartsWith("apache", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    foreach (var dir in directories)
                    {
                        var dirName = Path.GetFileName(dir);
                        
                        // Try to extract version from directory name
                        // Examples: "apache-2.4.65", "apache2.4.65", "apache24"
                        var versionMatch = Regex.Match(
                            dirName, 
                            @"apache[-]?(\d+\.\d+\.\d+|\d+\.\d+)", 
                            RegexOptions.IgnoreCase);

                        if (versionMatch.Success && File.Exists(Path.Combine(dir, "bin", "httpd.exe")))
                        {
                            return versionMatch.Groups[1].Value;
                        }
                    }
                }
            }

            return null;
        }
    }
}