using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ModStack.GUI.Services
{
    /// <summary>
    /// Service for managing Apache modules
    /// </summary>
    public class ApacheModuleService
    {
        private readonly ILogger<ApacheModuleService>? _logger;

        public ApacheModuleService(ILogger<ApacheModuleService>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Ensures that required Apache modules are enabled for virtual host generation
        /// </summary>
        public async Task EnsureRequiredModulesEnabledAsync(string apachePath)
        {
            try
            {
                Debug.WriteLine("🔧 Validating required Apache modules...");

                var httpdConfPath = Path.Combine(apachePath, "conf", "httpd.conf");
                if (!File.Exists(httpdConfPath))
                {
                    Debug.WriteLine("❌ Apache httpd.conf not found");
                    return;
                }

                var httpdConfContent = await File.ReadAllTextAsync(httpdConfPath);
                
                // Required modules for virtual host generation
                var requiredModules = new Dictionary<string, string>
                {
                    { "mod_fcgid", "fcgid_module" },
                    { "mod_ssl", "ssl_module" },
                    { "mod_rewrite", "rewrite_module" },
                    { "mod_headers", "headers_module" }
                };

                var modulesToEnable = new List<string>();
                var configUpdated = false;

                foreach (var module in requiredModules)
                {
                    var moduleName = module.Key;
                    var loadModuleName = module.Value;
                    
                    // Check if module is already loaded
                    if (!httpdConfContent.Contains($"LoadModule {loadModuleName}"))
                    {
                        Debug.WriteLine($"⚠️ Required module {moduleName} is not enabled");
                        modulesToEnable.Add(loadModuleName);
                    }
                    else
                    {
                        Debug.WriteLine($"✅ Module {moduleName} is already enabled");
                    }
                }

                // Enable missing modules
                if (modulesToEnable.Count > 0)
                {
                    Debug.WriteLine($"🔧 Enabling {modulesToEnable.Count} required modules...");
                    
                    var newLoadModules = new List<string>();
                    foreach (var module in modulesToEnable)
                    {
                        // Find the correct module file path
                        var modulePath = FindModulePath(apachePath, module);
                        if (!string.IsNullOrEmpty(modulePath))
                        {
                            newLoadModules.Add($"LoadModule {module} \"{modulePath}\"");
                            Debug.WriteLine($"✅ Will enable {module} from {modulePath}");
                        }
                        else
                        {
                            Debug.WriteLine($"⚠️ Could not find module file for {module}");
                        }
                    }

                    // Add the new LoadModule directives to the configuration
                    if (newLoadModules.Count > 0)
                    {
                        // Find a good place to insert the LoadModule directives
                        var insertPosition = httpdConfContent.IndexOf("# LoadModule foo_module modules/mod_foo.so");
                        if (insertPosition == -1)
                        {
                            // If no LoadModule section found, add after the first comment block
                            insertPosition = httpdConfContent.IndexOf("\n#\n", StringComparison.OrdinalIgnoreCase);
                            if (insertPosition != -1)
                            {
                                insertPosition = httpdConfContent.IndexOf('\n', insertPosition + 3) + 1;
                            }
                        }

                        if (insertPosition != -1)
                        {
                            var insertText = string.Join(Environment.NewLine, newLoadModules) + Environment.NewLine + Environment.NewLine;
                            httpdConfContent = httpdConfContent.Insert(insertPosition, insertText);
                            configUpdated = true;
                        }
                    }
                }

                // Save the updated configuration if changes were made
                if (configUpdated)
                {
                    await File.WriteAllTextAsync(httpdConfPath, httpdConfContent);
                    Debug.WriteLine("✅ Apache configuration updated with required modules");
                    
                    // Test the configuration before proceeding
                    await TestApacheConfigurationAsync(apachePath);
                }
                else
                {
                    Debug.WriteLine("✅ All required modules are already enabled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error ensuring required modules are enabled: {ex.Message}");
                _logger?.LogError(ex, "Failed to ensure required Apache modules are enabled");
                // Don't throw - this is not critical for virtual host creation
            }
        }

        /// <summary>
        /// Tests Apache configuration
        /// </summary>
        public async Task<bool> TestApacheConfigurationAsync(string apachePath)
        {
            try
            {
                Debug.WriteLine("🔍 Testing Apache configuration...");
                
                var httpdExe = Path.Combine(apachePath, "bin", "httpd.exe");
                if (!File.Exists(httpdExe))
                {
                    Debug.WriteLine("❌ Apache httpd.exe not found");
                    return false;
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = httpdExe,
                    Arguments = "-t",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode == 0)
                {
                    Debug.WriteLine("✅ Apache configuration test passed");
                    if (!string.IsNullOrEmpty(output))
                    {
                        Debug.WriteLine($"Output: {output}");
                    }
                    return true;
                }
                else
                {
                    Debug.WriteLine($"❌ Apache configuration test failed (exit code: {process.ExitCode})");
                    if (!string.IsNullOrEmpty(output))
                    {
                        Debug.WriteLine($"Output: {output}");
                    }
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.WriteLine($"Error: {error}");
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error testing Apache configuration: {ex.Message}");
                _logger?.LogError(ex, "Error testing Apache configuration");
                return false;
            }
        }

        /// <summary>
        /// Finds the path to an Apache module file
        /// </summary>
        private string? FindModulePath(string apachePath, string moduleName)
        {
            try
            {
                var modulesDir = Path.Combine(apachePath, "modules");
                if (!Directory.Exists(modulesDir))
                {
                    return null;
                }

                // Map module names to their file names
                var moduleFileMap = new Dictionary<string, string>
                {
                    { "fcgid_module", "mod_fcgid.so" },
                    { "ssl_module", "mod_ssl.so" },
                    { "rewrite_module", "mod_rewrite.so" },
                    { "headers_module", "mod_headers.so" }
                };

                if (moduleFileMap.TryGetValue(moduleName, out var fileName))
                {
                    var fullPath = Path.Combine(modulesDir, fileName);
                    if (File.Exists(fullPath))
                    {
                        return fullPath.Replace("\\", "/"); // Use forward slashes for Apache config
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error finding module path for {moduleName}: {ex.Message}");
                return null;
            }
        }
    }
}
